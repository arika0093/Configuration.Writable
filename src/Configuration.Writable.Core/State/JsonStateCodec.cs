using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Migration;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.State;

/// <summary>
/// Native JSON state codec. Replaces the <c>JsonFormatProvider</c> /
/// <c>JsonAotFormatProvider</c> pipeline with direct <see cref="IStateCodec{T}"/>
/// serialization over <see cref="FileStateResource{T}"/> file operations.
/// </summary>
internal sealed class JsonStateCodec<T> : FileStateCodecBase<T>
    where T : class, new()
{
#if NET
    private const string AotJsonReason =
        "JsonSerializerOptions.TypeInfoResolver handles NativeAOT scenarios";
#endif

    private readonly JsonSerializerOptions _effectiveOptions;
    private readonly bool _isAot;
    private readonly string _schemaVersionProperty;
    private readonly IReadOnlyList<string> _schemaVersionFallbackProperties;

    /// <param name="effectiveOptions">The JSON serializer options to use.</param>
    /// <param name="typeInfoResolver">
    /// The source-generated type info resolver, or <see langword="null"/> for runtime JSON metadata.
    /// </param>
    /// <param name="schemaVersionProperty">The property name used to persist the schema version.</param>
    /// <param name="schemaVersionFallbackProperties">Legacy property names accepted when reading versions.</param>
    internal JsonStateCodec(
        JsonSerializerOptions effectiveOptions,
        IJsonTypeInfoResolver? typeInfoResolver,
        string schemaVersionProperty,
        IReadOnlyList<string> schemaVersionFallbackProperties
    )
    {
        _effectiveOptions =
            effectiveOptions ?? throw new ArgumentNullException(nameof(effectiveOptions));
        _isAot = typeInfoResolver is not null;
        _schemaVersionProperty = schemaVersionProperty;
        _schemaVersionFallbackProperties = schemaVersionFallbackProperties;
    }

    protected override object LoadCore(
        FileStateResource<T> resource,
        string path,
        Type type,
        WritableOptionsConfiguration<T> options
    )
    {
        if (!resource.FileExists(path))
        {
            return CreateDefault(type);
        }

        using var stream = resource.OpenRead(path);
        if (options.SectionNameParts.Count == 0)
        {
            return DeserializeRoot(stream, type) ?? CreateDefault(type);
        }

        using var document = JsonDocument.Parse(stream, GetDocumentOptions());
        if (
            !JsonStateWriterHelper.TryNavigateToSection(
                document.RootElement,
                options.SectionNameParts,
                out var current
            )
        )
        {
            return CreateDefault(type);
        }

        return DeserializeText(current.GetRawText(), type) ?? CreateDefault(type);
    }

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private object? DeserializeRoot(Stream stream, Type type)
    {
        if (_isAot)
        {
            using var document = JsonDocument.Parse(stream, GetDocumentOptions());
            return JsonSerializer.Deserialize(
                document.RootElement.GetRawText(),
                _effectiveOptions.GetTypeInfo(type)
            );
        }

        return JsonSerializer.Deserialize(stream, type, _effectiveOptions);
    }

    private JsonDocumentOptions GetDocumentOptions() =>
        new()
        {
            AllowTrailingCommas = _effectiveOptions.AllowTrailingCommas,
            CommentHandling = _effectiveOptions.ReadCommentHandling,
            MaxDepth = _effectiveOptions.MaxDepth,
        };

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private object? DeserializeText(string json, Type type)
    {
        if (_isAot)
        {
            return JsonSerializer.Deserialize(json, _effectiveOptions.GetTypeInfo(type));
        }

        return JsonSerializer.Deserialize(json, type, _effectiveOptions);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "Non-AOT codecs retain runtime type activation; AOT callers use source-generated type info."
    )]
    private object CreateDefault(Type type)
    {
        if (_isAot)
        {
            return _effectiveOptions.GetTypeInfo(type).CreateObject?.Invoke()
                ?? throw new InvalidOperationException(
                    $"JSON type information for {type.Name} does not provide an object factory."
                );
        }

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create an instance of {type.Name}.");
    }

    protected override OptionsSchemaMetadata? ReadSchemaMetadata(
        FileStateResource<T> resource,
        string path,
        WritableOptionsConfiguration<T> options
    )
    {
        if (!resource.FileExists(path))
        {
            return null;
        }

        using var stream = resource.OpenRead(path);
        using var document = JsonDocument.Parse(stream, GetDocumentOptions());
        if (
            !JsonStateWriterHelper.TryNavigateToSection(
                document.RootElement,
                options.SectionNameParts,
                out var current
            )
        )
        {
            return null;
        }

        if (current.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Options schema metadata must be stored in a JSON object.");
        }

        var version = ReadOptionalVersion(current) ?? 1;
        return new OptionsSchemaMetadata(null, version);
    }

    private int? ReadOptionalVersion(JsonElement element)
    {
        foreach (
            var propertyName in new[] { _schemaVersionProperty }
                .Concat(_schemaVersionFallbackProperties)
                .Distinct(StringComparer.Ordinal)
        )
        {
            if (!TryGetMetadataProperty(element, propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var version))
            {
                throw new FormatException(
                    $"JSON metadata property '{propertyName}' must be an integer."
                );
            }

            return version;
        }

        return null;
    }

    private bool TryGetMetadataProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value
    )
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        var convertedName = _effectiveOptions.PropertyNamingPolicy?.ConvertName(propertyName);
        if (
            convertedName is not null
            && convertedName != propertyName
            && element.TryGetProperty(convertedName, out value)
        )
        {
            return true;
        }

        // The runtime provider also accepts case-insensitive matches; the AOT
        // provider only accepts exact and naming-policy converted names.
        if (!_isAot && _effectiveOptions.PropertyNameCaseInsensitive)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (
                    string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                    || (
                        convertedName is not null
                        && string.Equals(
                            property.Name,
                            convertedName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    protected override ReadOnlyMemory<byte> GetSaveContents(
        T config,
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    )
    {
        options.Logger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        var sections = options.SectionNameParts;
        var serializeAction = JsonStateWriterHelper.AddSchemaMetadata(
            CreateSerializeAction(),
            options.SchemaMetadata,
            _schemaVersionProperty,
            sections.Count == 0
                ? JsonSchemaGeneration.ResolveSchemaReference(
                    options.SchemaBaseUri,
                    options.SchemaMetadata
                )
                : null
        );
        var writerOptions = new JsonWriterOptions
        {
            Indented = _effectiveOptions.WriteIndented,
            Encoder = _effectiveOptions.Encoder,
        };

        if (sections.Count == 0)
        {
            return JsonStateWriterHelper.GetFullSaveContents(
                config,
                writerOptions,
                serializeAction,
                options.Logger
            );
        }

        options.Logger?.Log(
            LogLevel.Trace,
            "Using partial write for section: {SectionName}",
            string.Join(":", sections)
        );

        return JsonStateWriterHelper.GetPartialSaveContents(
            config,
            sections,
            writerOptions,
            serializeAction,
            path => resource.FileExists(path) ? resource.OpenRead(path) : null,
            options.ConfigFilePath,
            options.Logger
        );
    }

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private JsonSerializeAction<T> CreateSerializeAction()
    {
        if (_isAot)
        {
            var typeInfo = _effectiveOptions.GetTypeInfo(typeof(T));
            return (writer, value) => JsonSerializer.Serialize(writer, value, typeInfo);
        }

        var options = _effectiveOptions;
        return (writer, value) => JsonSerializer.Serialize(writer, value, options);
    }
}
