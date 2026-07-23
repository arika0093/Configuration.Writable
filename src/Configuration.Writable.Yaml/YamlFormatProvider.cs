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

/// <summary>
/// Writable configuration implementation for Yaml files using VYaml.
/// This provider is AOT-compatible when user types are annotated with <c>[YamlObject]</c>.
/// </summary>
public class YamlFormatProvider : FormatProviderBase
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
    public override string FileExtension => "yaml";

    /// <inheritdoc />
    public override async ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = reader.AsStream(leaveOpen: false);
        var yamlBytes = await ReadYamlBytesAsync(stream, cancellationToken).ConfigureAwait(false);
        if (IsEmptyOrWhiteSpace(yamlBytes.Span))
        {
            return Activator.CreateInstance(type)!;
        }

        var targetBytes = yamlBytes;

        if (sectionNameParts.Count > 0)
        {
            var data = YamlSerializer.Deserialize<Dictionary<string, object>>(
                yamlBytes,
                SerializerOptions
            );
            if (data == null)
            {
                return Activator.CreateInstance(type)!;
            }

            object? current = data;
            foreach (var section in sectionNameParts)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue(section, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        return Activator.CreateInstance(type)!;
                    }
                }
                else if (current is Dictionary<object, object> objDict)
                {
                    // VYaml deserializes nested maps as Dictionary<object, object>
                    if (TryGetSectionValue(objDict, section, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        return Activator.CreateInstance(type)!;
                    }
                }
                else
                {
                    return Activator.CreateInstance(type)!;
                }
            }

            targetBytes = YamlSerializer.Serialize(current, SerializerOptions);
        }

        try
        {
            var genericMethod = DeserializeMethods.GetOrAdd(
                type,
                static type => DeserializeMethod.MakeGenericMethod(type)
            );
            var result = genericMethod.Invoke(
                null,
                new object[] { targetBytes, SerializerOptions }
            );
            return result ?? Activator.CreateInstance(type)!;
        }
        catch
        {
            return Activator.CreateInstance(type)!;
        }
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
            // No section name, serialize directly (full file overwrite)
            return new ValueTask<ReadOnlyMemory<byte>>(SerializeForFile(config));
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

        // Try to read existing file using PipeReader
        try
        {
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            options.Logger?.ZLogWarning(
                ex,
                $"Failed to parse existing YAML file, will create new file structure"
            );
        }

        // Serialize config to YAML then deserialize to dictionary
        // This goes through YAML to avoid type boxing issues (e.g. decimal)
        var configYamlBytes = YamlSerializer.Serialize(config, SerializerOptions);
        var configDict =
            YamlSerializer.Deserialize<Dictionary<string, object>>(
                configYamlBytes,
                SerializerOptions
            ) ?? new Dictionary<string, object>();

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
