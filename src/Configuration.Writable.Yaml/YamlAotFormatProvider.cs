using System;
using System.Buffers;
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
/// AOT-compatible writable configuration implementation for YAML files using VYaml.
/// This provider uses VYaml's source-generated serialization to support Native AOT scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Initializes a new instance of the <see cref="YamlAotFormatProvider"/> class
/// with the specified serializer options.
/// </para>
/// </remarks>
/// <param name="serializerOptions">
/// The VYaml serializer options to use for serialization and deserialization.
/// This should be configured with appropriate resolvers for your types.
/// </param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="serializerOptions"/> is null.</exception>
public class YamlAotFormatProvider(YamlSerializerOptions serializerOptions) : FormatProviderBase
{
    private readonly YamlSerializerOptions _serializerOptions =
        serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));

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
        // Set the options for this operation
        var previousOptions = YamlSerializer.DefaultOptions;
        YamlSerializer.DefaultOptions = _serializerOptions;
        
        try
        {
            // Use PipeReader.AsStream for compatibility with StreamReader
            // The stream owns the PipeReader when leaveOpen is false
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

            var yamlUtf8Bytes = Encoding.GetBytes(yamlContent);

            // Navigate to the section if specified
            if (sectionNameParts.Count > 0)
            {
                // First deserialize to dynamic object to navigate sections
                var data = YamlSerializer.Deserialize<Dictionary<string, object>>(yamlUtf8Bytes);
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
                        if (objDict.TryGetValue(section, out var value))
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

                // Serialize the section back to YAML and deserialize to target type
                var sectionYamlBytes = YamlSerializer.Serialize(current);
                var result = DeserializeYaml(sectionYamlBytes, type);
                return result ?? Activator.CreateInstance(type)!;
            }

            // Deserialize YAML directly to the target type
            try
            {
                var result = DeserializeYaml(yamlUtf8Bytes, type);
                return result ?? Activator.CreateInstance(type)!;
            }
            catch (Exception)
            {
                return Activator.CreateInstance(type)!;
            }
        }
        finally
        {
            // Restore previous options
            YamlSerializer.DefaultOptions = previousOptions;
        }
    }

    /// <summary>
    /// Deserializes YAML bytes to the specified type using reflection.
    /// This is necessary because VYaml requires generic type parameters at compile time.
    /// </summary>
    private static object? DeserializeYaml(ReadOnlyMemory<byte> yamlBytes, Type type)
    {
        // Find the Deserialize<T>(ReadOnlyMemory<byte>) method
        var methods = typeof(YamlSerializer).GetMethods(BindingFlags.Public | BindingFlags.Static);
        var deserializeMethod = methods.FirstOrDefault(m =>
            m.Name == "Deserialize" &&
            m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType == typeof(ReadOnlyMemory<byte>)
        );

        if (deserializeMethod != null)
        {
            var genericMethod = deserializeMethod.MakeGenericMethod(type);
            return genericMethod.Invoke(null, new object[] { yamlBytes });
        }

        // Fallback: try with options parameter
        deserializeMethod = methods.FirstOrDefault(m =>
            m.Name == "Deserialize" &&
            m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 2 &&
            m.GetParameters()[0].ParameterType == typeof(ReadOnlyMemory<byte>)
        );

        if (deserializeMethod != null)
        {
            var genericMethod = deserializeMethod.MakeGenericMethod(type);
            return genericMethod.Invoke(null, new object?[] { yamlBytes, null });
        }

        return null;
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        WritableOptionsConfiguration<T> options,
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
    private ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var sections = options.SectionNameParts;

        // Set the options for this operation
        var previousOptions = YamlSerializer.DefaultOptions;
        YamlSerializer.DefaultOptions = _serializerOptions;

        try
        {
            if (sections.Count == 0)
            {
                // No section name, serialize directly (full file overwrite)
                options.Logger?.Log(
                    LogLevel.Trace,
                    "Serializing configuration of type {ConfigType} to YAML using VYaml AOT provider",
                    typeof(T).Name
                );

                var yamlBytes = YamlSerializer.Serialize(config);
                return yamlBytes;
            }
            else
            {
                // Section specified - use partial write (merge with existing file)
                return GetPartialSaveContents(config, options);
            }
        }
        finally
        {
            // Restore previous options
            YamlSerializer.DefaultOptions = previousOptions;
        }
    }

    /// <summary>
    /// Gets the save contents for partial write (when SectionName is specified).
    /// Reads existing file and merges the new configuration into the specified section.
    /// </summary>
    private ReadOnlyMemory<byte> GetPartialSaveContents<T>(
        T config,
        WritableOptionsConfiguration<T> options
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
                        var existingYamlBytes = Encoding.GetBytes(yamlContent);
                        existingDict = YamlSerializer.Deserialize<Dictionary<string, object>>(
                            existingYamlBytes
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

        // Serialize config to dictionary
        var configYamlBytes = YamlSerializer.Serialize(config);
        var configDict =
            YamlSerializer.Deserialize<Dictionary<string, object>>(
                configYamlBytes
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

            // Deep copy the existing dictionary to avoid modifying it
            resultDict = DeepCopyDictionary(existingDict);
            MergeSection(resultDict, sections, 0, configDict);
        }

        var yamlBytes = YamlSerializer.Serialize(resultDict);

        options.Logger?.ZLogTrace($"Partial YAML serialization completed successfully");

        return yamlBytes;
    }

    /// <summary>
    /// Creates a nested section structure from a list of section names.
    /// </summary>
    private static new object CreateNestedSection(List<string> sections, object value)
    {
        if (sections.Count == 0)
        {
            return value;
        }

        var result = new Dictionary<string, object>();
        var current = result;

        for (int i = 0; i < sections.Count - 1; i++)
        {
            var nested = new Dictionary<string, object>();
            current[sections[i]] = nested;
            current = nested;
        }

        current[sections[sections.Count - 1]] = value;
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
