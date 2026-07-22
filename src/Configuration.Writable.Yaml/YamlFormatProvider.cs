using System;
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
    private static readonly MethodInfo DeserializeMethod =
        typeof(YamlSerializer)
            .GetMethods()
            .First(m =>
                m.Name == nameof(YamlSerializer.Deserialize)
                && m.IsGenericMethod
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(ReadOnlyMemory<byte>)
            );

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
        var stream = reader.AsStream(leaveOpen: false);
#if NETSTANDARD2_0
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

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Activator.CreateInstance(type)!;
        }

        string targetYamlContent = yamlContent;

        if (sectionNameParts.Count > 0)
        {
            var yamlBytes = (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(yamlContent);
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
                    var key = objDict.Keys.FirstOrDefault(k => k?.ToString() == section);
                    if (key != null && objDict.TryGetValue(key, out var value))
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

            targetYamlContent = YamlSerializer.SerializeToString(
                current,
                SerializerOptions
            );
        }

        try
        {
            var targetBytes = (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(targetYamlContent);
            var genericMethod = DeserializeMethod.MakeGenericMethod(type);
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

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
        CancellationToken cancellationToken = default
    )
    {
        var contents = GetSaveContents(config, options);
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
    private ReadOnlyMemory<byte> GetSaveContents<T>(T config, IWritableOptionsConfiguration options)
        where T : class, new()
    {
        var sections = options.SectionNameParts;

        if (sections.Count == 0)
        {
            // No section name, serialize directly (full file overwrite)
            var yamlString = YamlSerializer.SerializeToString(config, SerializerOptions);
            return Encoding.GetBytes(yamlString);
        }
        else
        {
            // Section specified - use partial write (merge with existing file)
            return GetPartialSaveContents(config, options);
        }
    }

    /// <summary>
    /// Gets the save contents for partial write (when SectionName is specified).
    /// Reads existing file and merges the new configuration into the specified section.
    /// </summary>
    private ReadOnlyMemory<byte> GetPartialSaveContents<T>(
        T config,
        IWritableOptionsConfiguration options
    )
        where T : class, new()
    {
        var sections = options.SectionNameParts;
        Dictionary<string, object>? existingDict = null;

        // Try to read existing file using PipeReader
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            try
            {
                var pipeReader = options.FileProvider.GetFilePipeReader(options.ConfigFilePath);
                if (pipeReader != null)
                {
                    using var stream = pipeReader.AsStream(leaveOpen: false);
                    using var reader = new StreamReader(stream, Encoding);
                    var yamlContent = reader.ReadToEnd();

                    if (!string.IsNullOrWhiteSpace(yamlContent))
                    {
                        var yamlBytes = (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(yamlContent);
                        existingDict = YamlSerializer.Deserialize<Dictionary<string, object>>(
                            yamlBytes,
                            SerializerOptions
                        );
                        options.Logger?.ZLogTrace($"Loaded existing YAML file for partial update");
                    }
                }
            }
            catch (Exception ex)
            {
                options.Logger?.ZLogWarning(
                    ex,
                    $"Failed to parse existing YAML file, will create new file structure"
                );
            }
        }

        // Serialize config to YAML then deserialize to dictionary
        // This goes through YAML text to avoid type boxing issues (e.g. decimal)
        var configYaml = YamlSerializer.SerializeToString(config, SerializerOptions);
        var configYamlBytes = (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(configYaml);
        var configDict =
            YamlSerializer.Deserialize<Dictionary<string, object>>(configYamlBytes, SerializerOptions)
            ?? new Dictionary<string, object>();

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

            // Deep copy the existing dictionary to avoid modifying it
            resultDict = DeepCopyDictionary(existingDict);
            MergeSection(resultDict, sections, 0, configDict);
        }

        var yamlString = YamlSerializer.SerializeToString(resultDict, SerializerOptions);

        options.Logger?.ZLogTrace($"Partial YAML serialization completed successfully");

        return Encoding.GetBytes(yamlString);
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
