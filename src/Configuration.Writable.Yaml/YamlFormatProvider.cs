using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Writable configuration implementation for Yaml files using VYaml.
/// NativeAOT requires generated model formatters and an AOT-safe resolver for any collection types.
/// </summary>
public class YamlFormatProvider
    : FormatProviderBase,
        IOptionsSchemaMetadataProvider,
        IDeepMergeableFormatProvider
{
    private static readonly MethodInfo DeserializeMethod = typeof(YamlSerializer)
        .GetMethods()
        .First(m =>
            m.Name == nameof(YamlSerializer.Deserialize)
            && m.IsGenericMethod
            && m.GetParameters().Length == 2
            && m.GetParameters()[0].ParameterType == typeof(ReadOnlyMemory<byte>)
        );
    private static readonly ConcurrentDictionary<Type, MethodInfo> DeserializeMethods = new();
    private static readonly ConcurrentDictionary<
        Type,
        Func<ReadOnlyMemory<byte>, YamlSerializerOptions, object>
    > AotDeserializers = new();

    /// <summary>
    /// Registers a source-generated YAML deserializer for NativeAOT applications.
    /// </summary>
    internal static void Register<T>()
        where T : class, new()
    {
        AotDeserializers.TryAdd(
            typeof(T),
            static (bytes, options) => YamlSerializer.Deserialize<T>(bytes, options)
        );
    }

    internal override void RegisterType<T>() => Register<T>();

    /// <summary>
    /// Gets or sets the serializer options used for serialization and deserialization.
    /// </summary>
    public YamlSerializerOptions SerializerOptions { get; init; } = YamlSerializerOptions.Standard;

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public override string SchemaVersionProperty { get; set; } = "$version";

    /// <inheritdoc />
    public override IReadOnlyList<string> SchemaVersionFallbackProperties { get; set; } =
    ["Version"];

    /// <inheritdoc />
    public override string FileExtension => "yaml";

    /// <inheritdoc />
    public OptionsSchemaMetadata? ReadSchemaMetadata(IWritableOptionsConfiguration options)
    {
        var pipeReader = options.FileProvider.GetFilePipeReader(options.ConfigFilePath);
        if (pipeReader == null)
        {
            return null;
        }

        using var stream = pipeReader.AsStream(leaveOpen: false);
        var yamlBytes = ReadYamlBytesAsync(stream, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        if (IsEmptyOrWhiteSpace(yamlBytes.Span))
        {
            return null;
        }

        IYamlValue? data;
        try
        {
            data = ParseYaml(yamlBytes);
        }
        catch (Exception ex)
        {
            throw new FormatException("Failed to read YAML schema metadata.", ex);
        }
        if (data == null)
        {
            return null;
        }

        var current = data;
        foreach (var section in options.SectionNameParts)
        {
            if (
                current is not YamlMapping mapping
                || !mapping.Values.TryGetValue(section, out current)
            )
            {
                return null;
            }
        }

        if (current is not YamlMapping metadata)
        {
            throw new FormatException("Options schema metadata must be stored in a YAML mapping.");
        }

        var version = ReadOptionalVersion(metadata) ?? 1;
        return new OptionsSchemaMetadata(null, version);
    }

    private static bool TryGetMetadataValue(YamlMapping metadata, string name, out string value)
    {
        if (metadata.Values.TryGetValue(name, out var yamlValue) && yamlValue is YamlScalar scalar)
        {
            value = scalar.Value;
            return true;
        }

        var camelCaseName = char.ToLowerInvariant(name[0]) + name.Substring(1);
        if (
            metadata.Values.TryGetValue(camelCaseName, out yamlValue)
            && yamlValue is YamlScalar camelCaseScalar
        )
        {
            value = camelCaseScalar.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private int? ReadOptionalVersion(YamlMapping metadata)
    {
        foreach (
            var propertyName in new[] { SchemaVersionProperty }
                .Concat(SchemaVersionFallbackProperties)
                .Distinct(StringComparer.Ordinal)
        )
        {
            if (!TryGetMetadataValue(metadata, propertyName, out var value))
            {
                continue;
            }

            return ConvertVersion(value, propertyName);
        }

        return null;
    }

    private static int ConvertVersion(string value, string propertyName)
    {
        if (
            long.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var version
            )
        )
        {
            return version is >= int.MinValue and <= int.MaxValue
                ? (int)version
                : throw new FormatException(
                    $"YAML metadata property '{propertyName}' must be an integer."
                );
        }

        throw new FormatException($"YAML metadata property '{propertyName}' must be an integer.");
    }

    /// <inheritdoc />
    public override async ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var stream = reader.AsStream(leaveOpen: false);
            var yamlBytes = await ReadYamlBytesAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            if (IsEmptyOrWhiteSpace(yamlBytes.Span))
            {
                return CreateInstance(type);
            }

            var targetBytes = GetSectionBytes(yamlBytes, sectionNameParts);
            return targetBytes == null
                ? CreateInstance(type)
                : Deserialize(type, targetBytes.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FormatException("Failed to deserialize YAML configuration.", ex);
        }
    }

    /// <inheritdoc />
    public ValueTask<T> MergeConfigurationsAsync<T>(
        IReadOnlyList<ReadOnlyMemory<byte>> documents,
        IReadOnlyList<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        if (documents is null)
            throw new ArgumentNullException(nameof(documents));
        if (sectionNameParts is null)
            throw new ArgumentNullException(nameof(sectionNameParts));
        cancellationToken.ThrowIfCancellationRequested();
        Register<T>();
        var metadata = new T() as IGeneratedOptionsMergeMetadata;
        IDeepMergeNode? merged = null;
        foreach (var yamlBytes in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsEmptyOrWhiteSpace(yamlBytes.Span))
                continue;

            var current = ParseYaml(yamlBytes);
            foreach (var section in sectionNameParts)
            {
                if (
                    current is not YamlMapping mapping
                    || !mapping.Values.TryGetValue(section, out current)
                )
                {
                    current = null;
                    break;
                }
            }

            if (current is null)
                continue;

            merged = DeepMergeDocument.Merge(merged, ToDeepMergeNode(current), typeof(T), metadata);
        }

        if (merged is null)
            return new ValueTask<T>(new T());

        var mergedYaml = SerializeDeepMergeYaml(merged);
        return new ValueTask<T>((T)Deserialize(typeof(T), mergedYaml));
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "Non-AOT callers retain the existing runtime type activation fallback; NativeAOT callers register source-generated deserializers."
    )]
    private static object CreateInstance(Type type) => Activator.CreateInstance(type)!;

    private static ReadOnlyMemory<byte>? GetSectionBytes(
        ReadOnlyMemory<byte> yamlBytes,
        List<string> sectionNameParts
    )
    {
        if (sectionNameParts.Count == 0)
        {
            return yamlBytes;
        }

        var value = ParseYaml(yamlBytes);
        foreach (var section in sectionNameParts)
        {
            if (value is not YamlMapping mapping || !mapping.Values.TryGetValue(section, out value))
                return null;
        }

        return SerializeYaml(value);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "The reflection fallback is used only when a type was not explicitly registered for NativeAOT."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The reflection fallback is used only when a type was not explicitly registered for NativeAOT."
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2060",
        Justification = "The reflection fallback is used only when a type was not explicitly registered for NativeAOT."
    )]
    private object Deserialize(Type type, ReadOnlyMemory<byte> yamlBytes)
    {
        if (AotDeserializers.TryGetValue(type, out var deserializer))
        {
            return deserializer(yamlBytes, SerializerOptions);
        }

        var genericMethod = DeserializeMethods.GetOrAdd(
            type,
            static type => DeserializeMethod.MakeGenericMethod(type)
        );
        var result = genericMethod.Invoke(null, new object[] { yamlBytes, SerializerOptions });
        return result ?? Activator.CreateInstance(type)!;
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadYamlBytesAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        if (Encoding.CodePage == Encoding.UTF8.CodePage)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
            var yamlBytes = buffer.ToArray();
            if (!HasNonUtf8Bom(yamlBytes))
            {
                return yamlBytes;
            }

            using var encodedStream = new MemoryStream(yamlBytes, writable: false);
            return await ReadEncodedYamlBytesAsync(encodedStream, cancellationToken)
                .ConfigureAwait(false);
        }

        return await ReadEncodedYamlBytesAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadEncodedYamlBytesAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
#if NETSTANDARD2_0
        _ = cancellationToken;
        using var streamReader = new StreamReader(stream, Encoding);
#else
        using var streamReader = new StreamReader(
            stream,
            Encoding,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false
        );
#endif

#if NET8_0_OR_GREATER
        var yamlContent = await streamReader
            .ReadToEndAsync(cancellationToken)
            .ConfigureAwait(false);
#else
        var yamlContent = await streamReader.ReadToEndAsync().ConfigureAwait(false);
#endif
        return Encoding.UTF8.GetBytes(yamlContent);
    }

    private static bool HasNonUtf8Bom(ReadOnlySpan<byte> yaml)
    {
        return yaml.Length >= 2
            && (
                (yaml[0] == 0xff && yaml[1] == 0xfe)
                || (yaml[0] == 0xfe && yaml[1] == 0xff)
                || (
                    yaml.Length >= 4
                    && yaml[0] == 0x00
                    && yaml[1] == 0x00
                    && yaml[2] == 0xfe
                    && yaml[3] == 0xff
                )
            );
    }

    private static bool IsEmptyOrWhiteSpace(ReadOnlySpan<byte> yaml)
    {
        foreach (var value in yaml)
        {
            if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
        CancellationToken cancellationToken = default
    )
    {
        var contents = await GetSaveContentsAsync(config, options, cancellationToken)
            .ConfigureAwait(false);
        await options
            .FileProvider.SaveToFileAsync(
                options.ConfigFilePath,
                contents,
                options.Logger,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the save contents for the configuration.
    /// </summary>
    private ValueTask<ReadOnlyMemory<byte>> GetSaveContentsAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
        CancellationToken cancellationToken
    )
        where T : class, new()
    {
        var sections = options.SectionNameParts;

        if (sections.Count == 0)
        {
            var contents =
                options.SchemaMetadata == null
                    ? SerializeForFile(
                        config,
                        JsonSchemaGeneration.ResolveSchemaReference(
                            options.SchemaBaseUri,
                            options.SchemaMetadata
                        )
                    )
                    : SerializeForFile(
                        (IYamlValue)CreateSchemaMetadataDictionary(config, options.SchemaMetadata),
                        JsonSchemaGeneration.ResolveSchemaReference(
                            options.SchemaBaseUri,
                            options.SchemaMetadata
                        )
                    );
            return new ValueTask<ReadOnlyMemory<byte>>(contents);
        }

        // Section specified - use partial write (merge with existing file)
        return GetPartialSaveContentsAsync(config, options, cancellationToken);
    }

    /// <summary>
    /// Gets the save contents for partial write (when SectionName is specified).
    /// Reads existing file and merges the new configuration into the specified section.
    /// </summary>
    private async ValueTask<ReadOnlyMemory<byte>> GetPartialSaveContentsAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
        CancellationToken cancellationToken
    )
        where T : class, new()
    {
        var sections = options.SectionNameParts;
        YamlMapping? existingDocument = null;

        // A malformed existing file must never be replaced with a new partial document.
        // Propagating the parse error preserves the original file for recovery.
        var pipeReader = options.FileProvider.GetFilePipeReader(options.ConfigFilePath);
        if (pipeReader != null)
        {
            using var stream = pipeReader.AsStream(leaveOpen: false);
            var yamlBytes = await ReadYamlBytesAsync(stream, cancellationToken)
                .ConfigureAwait(false);

            if (!IsEmptyOrWhiteSpace(yamlBytes.Span))
            {
                existingDocument =
                    ParseYaml(yamlBytes) as YamlMapping
                    ?? throw new FormatException("YAML document must be a mapping.");
                options.Logger?.LogTrace("Loaded existing YAML file for partial update");
            }
        }

        // Serialize config to YAML then deserialize to dictionary
        // This goes through YAML to avoid type boxing issues (e.g. decimal)
        var configYamlBytes = YamlSerializer.Serialize(config, SerializerOptions);
        var configValue = ParseYaml(configYamlBytes);
        if (configValue is not YamlMapping configMapping)
            throw new FormatException("YAML configuration must be a mapping.");
        AddSchemaMetadata(configMapping, options.SchemaMetadata, SchemaVersionProperty);

        YamlMapping resultDocument;

        if (existingDocument == null)
        {
            // No existing file, create new nested structure
            options.Logger?.LogTrace(
                "Creating new nested section structure for section: {Section}",
                string.Join(":", sections)
            );

            resultDocument = CreateNestedSection(sections, configMapping);
        }
        else
        {
            // Merge with existing document
            options.Logger?.LogTrace(
                "Merging with existing YAML file for section: {Section}",
                string.Join(":", sections)
            );

            resultDocument = existingDocument;
            MergeSection(resultDocument, sections, 0, configMapping);
        }

        options.Logger?.LogTrace("Partial YAML serialization completed successfully");

        return SerializeForFile((IYamlValue)resultDocument);
    }

    private ReadOnlyMemory<byte> SerializeForFile<T>(T value, string? schemaReference = null)
    {
        var utf8Bytes = YamlSerializer.Serialize(value, SerializerOptions);
        return AddSchemaReference(utf8Bytes, schemaReference);
    }

    private ReadOnlyMemory<byte> SerializeForFile(IYamlValue value, string? schemaReference = null)
    {
        return AddSchemaReference(SerializeYaml(value), schemaReference);
    }

    private ReadOnlyMemory<byte> AddSchemaReference(
        ReadOnlyMemory<byte> utf8Bytes,
        string? schemaReference
    )
    {
        string yaml;
#if NETSTANDARD2_0
        yaml = Encoding.UTF8.GetString(utf8Bytes.ToArray());
#else
        yaml = Encoding.UTF8.GetString(utf8Bytes.Span);
#endif
        if (schemaReference is not null)
            yaml =
                "# yaml-language-server: $schema=" + schemaReference + Environment.NewLine + yaml;
        return Encoding.GetBytes(yaml);
    }

    private YamlMapping CreateSchemaMetadataDictionary<T>(T config, OptionsSchemaMetadata metadata)
        where T : class, new()
    {
        var yamlBytes = YamlSerializer.Serialize(config, SerializerOptions);
        var mapping =
            ParseYaml(yamlBytes) as YamlMapping
            ?? throw new FormatException(
                "Options schema metadata can only be written for YAML mappings."
            );
        AddSchemaMetadata(mapping, metadata, SchemaVersionProperty);
        return mapping;
    }

    private static void AddSchemaMetadata(
        YamlMapping values,
        OptionsSchemaMetadata? metadata,
        string schemaVersionProperty
    )
    {
        if (metadata == null)
        {
            return;
        }

        if (metadata.Version is not null)
        {
            values.Values[schemaVersionProperty] = new YamlScalar(
                metadata.Version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        }
    }

    /// <summary>
    /// Merges a new configuration into an existing mapping at the specified section path.
    /// </summary>
    private static void MergeSection(
        YamlMapping existingMapping,
        List<string> sections,
        int currentIndex,
        IYamlValue newValue
    )
    {
        if (currentIndex >= sections.Count)
        {
            return;
        }

        var sectionName = sections[currentIndex];

        if (currentIndex == sections.Count - 1)
        {
            // This is the final section - replace or add the value
            existingMapping.Values[sectionName] = newValue;
        }
        else
        {
            if (!existingMapping.Values.TryGetValue(sectionName, out var existing))
            {
                existing = new YamlMapping();
                existingMapping.Values[sectionName] = existing;
            }

            if (existing is not YamlMapping nestedMapping)
            {
                nestedMapping = new YamlMapping();
                existingMapping.Values[sectionName] = nestedMapping;
            }

            MergeSection(nestedMapping, sections, currentIndex + 1, newValue);
        }
    }

    private static YamlMapping CreateNestedSection(
        IReadOnlyList<string> sections,
        YamlMapping value
    )
    {
        YamlMapping current = value;
        for (var index = sections.Count - 1; index >= 0; index--)
        {
            current = new YamlMapping { Values = { [sections[index]] = current } };
        }

        return current;
    }

    private static IYamlValue ParseYaml(ReadOnlyMemory<byte> yamlBytes)
    {
        var parser = YamlParser.FromSequence(new ReadOnlySequence<byte>(yamlBytes));
        parser.SkipHeader();
        return ParseValue(ref parser);
    }

    private static IYamlValue ParseValue(ref YamlParser parser)
    {
        return parser.CurrentEventType switch
        {
            ParseEventType.Scalar => ParseScalar(ref parser),
            ParseEventType.MappingStart => ParseMapping(ref parser),
            ParseEventType.SequenceStart => ParseSequence(ref parser),
            _ => throw new FormatException($"Unexpected YAML event: {parser.CurrentEventType}."),
        };
    }

    private static YamlScalar ParseScalar(ref YamlParser parser)
    {
        var scalar = parser.GetScalarAsString();
        var value = scalar ?? "null";
        var kind = GetScalarKind(ref parser);
        parser.ReadWithVerify(ParseEventType.Scalar);
        return new YamlScalar(value, kind);
    }

    private static DeepMergeScalarKind GetScalarKind(ref YamlParser parser)
    {
        if (parser.IsNullScalar())
            return DeepMergeScalarKind.Null;
        if (parser.TryGetScalarAsBool(out _))
            return DeepMergeScalarKind.Boolean;
        if (parser.TryGetScalarAsDouble(out _))
            return DeepMergeScalarKind.Number;
        return DeepMergeScalarKind.String;
    }

    private static IDeepMergeNode ToDeepMergeNode(IYamlValue value) =>
        value switch
        {
            YamlScalar scalar => new DeepMergeScalarNode(scalar.Kind, scalar.Value),
            YamlMapping mapping => ToDeepMergeObject(mapping),
            YamlSequence sequence => ToDeepMergeArray(sequence),
            _ => throw new InvalidOperationException($"Unsupported YAML value: {value.GetType()}."),
        };

    private static DeepMergeObjectNode ToDeepMergeObject(YamlMapping mapping)
    {
        var result = new DeepMergeObjectNode();
        foreach (var property in mapping.Values)
            result.Properties[property.Key] = ToDeepMergeNode(property.Value);
        return result;
    }

    private static DeepMergeArrayNode ToDeepMergeArray(YamlSequence sequence)
    {
        var result = new DeepMergeArrayNode();
        foreach (var item in sequence.Values)
            result.Items.Add(ToDeepMergeNode(item));
        return result;
    }

    private static ReadOnlyMemory<byte> SerializeDeepMergeYaml(IDeepMergeNode node)
    {
        var writer = new ByteBufferWriter();
        var emitter = new Utf8YamlEmitter(writer);
        WriteDeepMergeYaml(ref emitter, node);
        return writer.WrittenMemory;
    }

    private static void WriteDeepMergeYaml(ref Utf8YamlEmitter emitter, IDeepMergeNode node)
    {
        switch (node)
        {
            case DeepMergeScalarNode { Kind: DeepMergeScalarKind.String } scalar:
                emitter.WriteString(scalar.Value);
                break;
            case DeepMergeScalarNode scalar:
                emitter.WriteScalar(Encoding.UTF8.GetBytes(scalar.Value));
                break;
            case DeepMergeObjectNode objectNode:
                emitter.BeginMapping();
                foreach (var property in objectNode.Properties)
                {
                    emitter.WriteString(property.Key);
                    WriteDeepMergeYaml(ref emitter, property.Value);
                }
                emitter.EndMapping();
                break;
            case DeepMergeArrayNode arrayNode:
                emitter.BeginSequence();
                foreach (var item in arrayNode.Items)
                    WriteDeepMergeYaml(ref emitter, item);
                emitter.EndSequence();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported merge document node: {node.GetType()}."
                );
        }
    }

    private static YamlMapping ParseMapping(ref YamlParser parser)
    {
        var mapping = new YamlMapping();
        parser.ReadWithVerify(ParseEventType.MappingStart);
        while (!parser.End && parser.CurrentEventType != ParseEventType.MappingEnd)
        {
            if (parser.CurrentEventType != ParseEventType.Scalar)
                throw new FormatException("YAML mapping keys must be scalar values.");

            var key = parser.GetScalarAsString() ?? string.Empty;
            parser.ReadWithVerify(ParseEventType.Scalar);
            mapping.Values[key] = ParseValue(ref parser);
        }

        parser.ReadWithVerify(ParseEventType.MappingEnd);
        return mapping;
    }

    private static YamlSequence ParseSequence(ref YamlParser parser)
    {
        var sequence = new YamlSequence();
        parser.ReadWithVerify(ParseEventType.SequenceStart);
        while (!parser.End && parser.CurrentEventType != ParseEventType.SequenceEnd)
        {
            sequence.Values.Add(ParseValue(ref parser));
        }

        parser.ReadWithVerify(ParseEventType.SequenceEnd);
        return sequence;
    }

    private static ReadOnlyMemory<byte> SerializeYaml(IYamlValue value)
    {
        var writer = new ByteBufferWriter();
        var emitter = new Utf8YamlEmitter(writer);
        WriteYaml(ref emitter, value);
        return writer.WrittenMemory;
    }

    private sealed class ByteBufferWriter : IBufferWriter<byte>
    {
        private byte[] buffer = new byte[256];
        private int written;

        public ReadOnlyMemory<byte> WrittenMemory => buffer.AsMemory(0, written);

        public void Advance(int count)
        {
            if (count < 0 || count > buffer.Length - written)
                throw new ArgumentOutOfRangeException(nameof(count));

            written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return buffer.AsMemory(written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return buffer.AsSpan(written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            var requiredLength = written + Math.Max(sizeHint, 1);
            if (requiredLength <= buffer.Length)
                return;

            var newLength = Math.Max(requiredLength, buffer.Length * 2);
            Array.Resize(ref buffer, newLength);
        }
    }

    private static void WriteYaml(ref Utf8YamlEmitter emitter, IYamlValue value)
    {
        switch (value)
        {
            case YamlScalar scalar:
                emitter.WriteScalar(Encoding.UTF8.GetBytes(scalar.Value));
                break;
            case YamlMapping mapping:
                emitter.BeginMapping();
                foreach (var item in mapping.Values)
                {
                    emitter.WriteString(item.Key);
                    WriteYaml(ref emitter, item.Value);
                }
                emitter.EndMapping();
                break;
            case YamlSequence sequence:
                emitter.BeginSequence();
                foreach (var item in sequence.Values)
                {
                    WriteYaml(ref emitter, item);
                }
                emitter.EndSequence();
                break;
            default:
                throw new InvalidOperationException($"Unsupported YAML value: {value.GetType()}.");
        }
    }

    private interface IYamlValue;

    private sealed class YamlScalar(
        string value,
        DeepMergeScalarKind kind = DeepMergeScalarKind.String
    ) : IYamlValue
    {
        public string Value { get; } = value;
        public DeepMergeScalarKind Kind { get; } = kind;
    }

    private sealed class YamlMapping : IYamlValue
    {
        public Dictionary<string, IYamlValue> Values { get; } = new(StringComparer.Ordinal);
    }

    private sealed class YamlSequence : IYamlValue
    {
        public List<IYamlValue> Values { get; } = [];
    }
}
