using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VYaml.Serialization;
using ZLogger;

namespace Configuration.Writable.FormatProvider;

#pragma warning disable CS0618 // IHasVersion remains supported for backward compatibility.
/// <summary>
/// Writable configuration implementation for Yaml files using VYaml.
/// This provider is AOT-compatible when user types are annotated with <c>[YamlObject]</c>.
/// </summary>
public class YamlFormatProvider : FormatProviderBase, IOptionsSchemaMetadataProvider
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
    public override IReadOnlyList<string> SchemaVersionFallbackProperties { get; set; } = ["Version"];

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

        Dictionary<string, object>? data;
        try
        {
            data = YamlSerializer.Deserialize<Dictionary<string, object>>(
                yamlBytes,
                SerializerOptions
            );
        }
        catch (Exception ex)
        {
            throw new FormatException("Failed to read YAML schema metadata.", ex);
        }
        if (data == null)
        {
            return null;
        }

        object current = data;
        foreach (var section in options.SectionNameParts)
        {
            if (!TryGetSectionValue(current, section, out var sectionValue) || sectionValue == null)
            {
                return null;
            }
            current = sectionValue;
        }

        if (current is not Dictionary<string, object> metadata)
        {
            if (current is Dictionary<object, object> objectMetadata)
            {
                metadata = DeepCopyObjectDictionary(objectMetadata);
            }
            else
            {
                throw new FormatException(
                    "Options schema metadata must be stored in a YAML mapping."
                );
            }
        }

        var version = ReadOptionalVersion(metadata) ?? 1;
        return new OptionsSchemaMetadata(null, version);
    }

    private static bool TryGetMetadataValue(
        Dictionary<string, object> metadata,
        string name,
        out object value
    )
    {
        if (metadata.TryGetValue(name, out value!))
        {
            return true;
        }

        var camelCaseName = char.ToLowerInvariant(name[0]) + name.Substring(1);
        return metadata.TryGetValue(camelCaseName, out value!);
    }

    private int? ReadOptionalVersion(Dictionary<string, object> metadata)
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

    private static int ConvertVersion(object? value, string propertyName)
    {
        switch (value)
        {
            case sbyte version:
                return version;
            case byte version:
                return version;
            case short version:
                return version;
            case ushort version:
                return version;
            case int version:
                return version;
            case uint version when version <= int.MaxValue:
                return (int)version;
            case long version when version is >= int.MinValue and <= int.MaxValue:
                return (int)version;
            case ulong version when version <= int.MaxValue:
                return (int)version;
            default:
                throw new FormatException(
                    $"YAML metadata property '{propertyName}' must be an integer."
                );
        }
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
                return Activator.CreateInstance(type)!;
            }

            var targetBytes = GetSectionBytes(yamlBytes, sectionNameParts);
            return targetBytes == null
                ? Activator.CreateInstance(type)!
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

    private ReadOnlyMemory<byte>? GetSectionBytes(
        ReadOnlyMemory<byte> yamlBytes,
        List<string> sectionNameParts
    )
    {
        if (sectionNameParts.Count == 0)
        {
            return yamlBytes;
        }

        var data = YamlSerializer.Deserialize<Dictionary<string, object>>(
            yamlBytes,
            SerializerOptions
        );
        if (data == null || !TryGetSectionValue(data, sectionNameParts, out var value))
        {
            return null;
        }

        return YamlSerializer.Serialize(value, SerializerOptions);
    }

    private object Deserialize(Type type, ReadOnlyMemory<byte> yamlBytes)
    {
        var genericMethod = DeserializeMethods.GetOrAdd(
            type,
            static type => DeserializeMethod.MakeGenericMethod(type)
        );
        var result = genericMethod.Invoke(null, new object[] { yamlBytes, SerializerOptions });
        return result ?? Activator.CreateInstance(type)!;
    }

    private static bool TryGetSectionValue(
        Dictionary<string, object> data,
        IEnumerable<string> sectionNameParts,
        out object? value
    )
    {
        object? current = data;
        foreach (var section in sectionNameParts)
        {
            if (!TryGetSectionValue(current, section, out current))
            {
                value = null;
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool TryGetSectionValue(object? value, string section, out object? sectionValue)
    {
        if (value is Dictionary<string, object> dictionary)
        {
            return dictionary.TryGetValue(section, out sectionValue);
        }

        if (value is Dictionary<object, object> objectDictionary)
        {
            return TryGetSectionValue(objectDictionary, section, out sectionValue);
        }

        sectionValue = null;
        return false;
    }

    private static bool TryGetSectionValue(
        Dictionary<object, object> dictionary,
        string section,
        out object? value
    )
    {
        if (dictionary.TryGetValue(section, out value))
        {
            return true;
        }

        using var enumerator = dictionary.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            if (
                item.Key is not string
                && string.Equals(item.Key?.ToString(), section, StringComparison.Ordinal)
            )
            {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
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
                    ? SerializeForFile(config)
                    : SerializeForFile(
                        CreateSchemaMetadataDictionary(config, options.SchemaMetadata)
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
        Dictionary<string, object>? existingDict = null;

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
                existingDict = YamlSerializer.Deserialize<Dictionary<string, object>>(
                    yamlBytes,
                    SerializerOptions
                );
                options.Logger?.ZLogTrace($"Loaded existing YAML file for partial update");
            }
        }

        // Serialize config to YAML then deserialize to dictionary
        // This goes through YAML to avoid type boxing issues (e.g. decimal)
        var configYamlBytes = YamlSerializer.Serialize(config, SerializerOptions);
        var configDict =
            YamlSerializer.Deserialize<Dictionary<string, object>>(
                configYamlBytes,
                SerializerOptions
            ) ?? new Dictionary<string, object>();
        configDict = AddSchemaMetadata(
            configDict,
            options.SchemaMetadata,
            config is not IHasVersion,
            SchemaVersionProperty
        );

        Dictionary<string, object> resultDict;

        if (existingDict == null)
        {
            // No existing file, create new nested structure
            options.Logger?.ZLogTrace(
                $"Creating new nested section structure for section: {string.Join(":", sections)}"
            );

            var nestedSectionValue = CreateNestedSection(sections, configDict);
            resultDict =
                nestedSectionValue as Dictionary<string, object>
                ?? new Dictionary<string, object>();
        }
        else
        {
            // Merge with existing document
            options.Logger?.ZLogTrace(
                $"Merging with existing YAML file for section: {string.Join(":", sections)}"
            );

            resultDict = existingDict;
            MergeSection(resultDict, sections, 0, configDict);
        }

        options.Logger?.ZLogTrace($"Partial YAML serialization completed successfully");

        return SerializeForFile(resultDict);
    }

    private ReadOnlyMemory<byte> SerializeForFile<T>(T value)
    {
        var utf8Bytes = YamlSerializer.Serialize(value, SerializerOptions);
        if (Encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return utf8Bytes;
        }

#if NETSTANDARD2_0
        var yaml = Encoding.UTF8.GetString(utf8Bytes.ToArray());
#else
        var yaml = Encoding.UTF8.GetString(utf8Bytes.Span);
#endif
        return Encoding.GetBytes(yaml);
    }

    private Dictionary<string, object> CreateSchemaMetadataDictionary<T>(
        T config,
        OptionsSchemaMetadata metadata
    )
        where T : class, new()
    {
        var yamlBytes = YamlSerializer.Serialize(config, SerializerOptions);
        var dictionary =
            YamlSerializer.Deserialize<Dictionary<string, object>>(yamlBytes, SerializerOptions)
            ?? throw new FormatException(
                "Options schema metadata can only be written for YAML mappings."
            );
        return AddSchemaMetadata(
            dictionary,
            metadata,
            config is not IHasVersion,
            SchemaVersionProperty
        );
    }

    private static Dictionary<string, object> AddSchemaMetadata(
        Dictionary<string, object> values,
        OptionsSchemaMetadata? metadata,
        bool persistVersion,
        string schemaVersionProperty
    )
    {
        if (metadata == null)
        {
            return values;
        }

        var result = new Dictionary<string, object>();
        if (persistVersion && metadata.Version is not null)
        {
            result[schemaVersionProperty] = metadata.Version.Value;
        }
        foreach (var item in values)
        {
            result[item.Key] = item.Value;
        }
        return result;
    }

    /// <summary>
    /// Deep copies a dictionary, including nested dictionaries.
    /// </summary>
    private static Dictionary<string, object> DeepCopyDictionary(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in source)
        {
            if (kvp.Value is Dictionary<string, object> nestedStringDict)
            {
                result[kvp.Key] = DeepCopyDictionary(nestedStringDict);
            }
            else if (kvp.Value is Dictionary<object, object> nestedObjectDict)
            {
                result[kvp.Key] = DeepCopyObjectDictionary(nestedObjectDict);
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// Deep copies a Dictionary&lt;object, object&gt; to Dictionary&lt;string, object&gt;.
    /// </summary>
    private static Dictionary<string, object> DeepCopyObjectDictionary(
        Dictionary<object, object> source
    )
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in source)
        {
            var key = kvp.Key.ToString() ?? string.Empty;
            if (kvp.Value is Dictionary<object, object> nestedDict)
            {
                result[key] = DeepCopyObjectDictionary(nestedDict);
            }
            else if (kvp.Value is Dictionary<string, object> nestedStringDict)
            {
                result[key] = DeepCopyDictionary(nestedStringDict);
            }
            else
            {
                result[key] = kvp.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// Merges a new configuration into an existing dictionary at the specified section path.
    /// </summary>
    private static void MergeSection(
        Dictionary<string, object> existingDict,
        List<string> sections,
        int currentIndex,
        object newValue
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
            existingDict[sectionName] = newValue;
        }
        else
        {
            // Navigate deeper or create intermediate sections
            object? existing = null;
            Dictionary<string, object>? nestedDict = null;

            if (existingDict.TryGetValue(sectionName, out existing))
            {
                if (existing is Dictionary<string, object> stringDict)
                {
                    nestedDict = stringDict;
                }
                else if (existing is Dictionary<object, object> objectDict)
                {
                    // Convert Dictionary<object, object> to Dictionary<string, object>
                    nestedDict = DeepCopyObjectDictionary(objectDict);
                    existingDict[sectionName] = nestedDict;
                }
            }

            if (nestedDict == null)
            {
                // Create new nested dictionary
                nestedDict = new Dictionary<string, object>();
                existingDict[sectionName] = nestedDict;
            }

            MergeSection(nestedDict, sections, currentIndex + 1, newValue);
        }
    }
}
