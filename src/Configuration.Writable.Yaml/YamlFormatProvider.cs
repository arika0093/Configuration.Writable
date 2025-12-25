using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Writable configuration implementation for Yaml files.
/// </summary>
public class YamlFormatProvider : FormatProviderBase
{
    /// <summary>
    /// Gets or sets the serializer used to convert objects to and from a specific format.
    /// </summary>
    public ISerializer Serializer { get; init; } =
        new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

    /// <summary>
    /// Gets or sets the deserializer used to convert YAML to objects.
    /// </summary>
    public IDeserializer Deserializer { get; init; } =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public override string FileExtension => "yaml";

    /// <inheritdoc />
    public override T LoadConfiguration<T>(WritableOptionsConfiguration<T> options)
    {
        var filePath = options.ConfigFilePath;
        if (!options.FileProvider.FileExists(filePath))
        {
            return new T();
        }

        var stream = options.FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return new T();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableOptionsConfiguration<T> options)
    {
        using var reader = new StreamReader(stream, Encoding);
        var yamlContent = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return new T();
        }

        // For migration support, we need to:
        // 1. Parse YAML to extract the version
        // 2. Deserialize the YAML content to the correct type based on version
        // 3. Apply migrations if needed

        var sections = options.SectionNameParts;
        string targetYamlContent = yamlContent;

        // Navigate to section if specified
        if (sections.Count > 0)
        {
            var yamlObject = Deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            if (yamlObject == null)
            {
                return new T();
            }

            object current = yamlObject;
            foreach (var section in sections)
            {
                if (current is Dictionary<string, object> stringKeyDict)
                {
                    var key = stringKeyDict.Keys.FirstOrDefault(k =>
                        string.Equals(k, section, StringComparison.OrdinalIgnoreCase)
                    );
                    if (key != null && stringKeyDict.TryGetValue(key, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        return new T();
                    }
                }
                else if (current is Dictionary<object, object> objectKeyDict)
                {
                    var key = objectKeyDict.Keys.OfType<string>().FirstOrDefault(k =>
                        string.Equals(k, section, StringComparison.OrdinalIgnoreCase)
                    );
                    if (key != null && objectKeyDict.TryGetValue(key, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        return new T();
                    }
                }
                else
                {
                    return new T();
                }
            }

            // Serialize the section back to YAML
            targetYamlContent = Serializer.Serialize(current);
        }

        // Use migration-aware loading
        return LoadConfigurationWithMigration(
            targetYamlContent,
            options,
            yaml =>
            {
                // Try to deserialize as dictionary to extract version
                try
                {
                    var dict = Deserializer.Deserialize<Dictionary<string, object>>(yaml);
                    if (dict != null)
                    {
                        if (dict.TryGetValue("Version", out var versionObj)
                            && versionObj != null
                            && TryConvertToInt(versionObj, out var version))
                        {
                            return version;
                        }
                        if (dict.TryGetValue("version", out versionObj)
                            && versionObj != null
                            && TryConvertToInt(versionObj, out version))
                        {
                            return version;
                        }
                    }
                }
                catch
                {
                    // Ignore deserialization errors for version detection
                }
                return null;
            },
            (yaml, type) =>
            {
                // Deserialize YAML directly to the target type
                // Use a deserializer without naming convention for direct type deserialization
                var plainDeserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                try
                {
                    var result = plainDeserializer.Deserialize(yaml, type);
                    return result ?? Activator.CreateInstance(type)!;
                }
                catch (Exception)
                {
                    // If plain deserialization fails, try with the configured deserializer
                    try
                    {
                        var result = Deserializer.Deserialize(yaml, type);
                        return result ?? Activator.CreateInstance(type)!;
                    }
                    catch
                    {
                        return Activator.CreateInstance(type)!;
                    }
                }
            }
        );
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

        if (sections.Count == 0)
        {
            // No section name, serialize directly (full file overwrite)
            var serializer = Serializer;
            var yamlString = serializer.Serialize(config);
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
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var sections = options.SectionNameParts;
        Dictionary<string, object>? existingDict = null;

        // Try to read existing file
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            try
            {
                using var fileStream = options.FileProvider.GetFileStream(options.ConfigFilePath);
                if (fileStream != null && fileStream.Length > 0)
                {
                    using var reader = new StreamReader(fileStream, Encoding);
                    var yamlContent = reader.ReadToEnd();

                    if (!string.IsNullOrWhiteSpace(yamlContent))
                    {
                        existingDict = Deserializer.Deserialize<Dictionary<string, object>>(
                            yamlContent
                        );
                        options.Logger?.LogTrace("Loaded existing YAML file for partial update");
                    }
                }
            }
            catch (Exception ex)
            {
                options.Logger?.LogWarning(
                    ex,
                    "Failed to parse existing YAML file, will create new file structure"
                );
            }
        }

        var serializer = Serializer;
        var deserializer = Deserializer;

        // Serialize config to dictionary
        var configYaml = serializer.Serialize(config);
        var configDict =
            deserializer.Deserialize<Dictionary<string, object>>(configYaml)
            ?? new Dictionary<string, object>();

        Dictionary<string, object> resultDict;

        if (existingDict == null)
        {
            // No existing file, create new nested structure
            options.Logger?.LogTrace(
                "Creating new nested section structure for section: {SectionName}",
                string.Join(":", sections)
            );

            var nestedSectionValue = CreateNestedSection(sections, configDict);
            resultDict =
                nestedSectionValue as Dictionary<string, object>
                ?? new Dictionary<string, object>();
        }
        else
        {
            // Merge with existing document
            options.Logger?.LogTrace(
                "Merging with existing YAML file for section: {SectionName}",
                string.Join(":", sections)
            );

            // Deep copy the existing dictionary to avoid modifying it
            resultDict = DeepCopyDictionary(existingDict);
            MergeSection(resultDict, sections, 0, configDict);
        }

        var yamlString = serializer.Serialize(resultDict);

        options.Logger?.LogTrace("Partial YAML serialization completed successfully");

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

    /// <summary>
    /// Normalizes dictionary keys from camelCase to PascalCase for proper JSON deserialization.
    /// </summary>
    private static object NormalizeDictionaryKeys(object obj)
    {
        if (obj is Dictionary<string, object> stringDict)
        {
            var normalized = new Dictionary<string, object>();
            foreach (var kvp in stringDict)
            {
                // Convert camelCase key to PascalCase
                var normalizedKey = ToPascalCase(kvp.Key);
                normalized[normalizedKey] = NormalizeDictionaryKeys(kvp.Value);
            }
            return normalized;
        }

        if (obj is Dictionary<object, object> objectDict)
        {
            var normalized = new Dictionary<string, object>();
            foreach (var kvp in objectDict)
            {
                var key = kvp.Key.ToString() ?? string.Empty;
                var normalizedKey = ToPascalCase(key);
                normalized[normalizedKey] = NormalizeDictionaryKeys(kvp.Value);
            }
            return normalized;
        }

        // For lists/arrays, normalize each element
        if (obj is System.Collections.IList list)
        {
            var normalized = new List<object>();
            foreach (var item in list)
            {
                normalized.Add(NormalizeDictionaryKeys(item));
            }
            return normalized;
        }

        // For other types, return as-is
        return obj;
    }

    /// <summary>
    /// Converts a camelCase string to PascalCase.
    /// </summary>
    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Convert first character to uppercase
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// Tries to convert an object to an integer, handling various numeric types.
    /// </summary>
    private static bool TryConvertToInt(object value, out int result)
    {
        result = 0;

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }

        if (value is double doubleValue && doubleValue >= int.MinValue
            && doubleValue <= int.MaxValue
            && Math.Abs(doubleValue % 1) < double.Epsilon)
        {
            result = (int)doubleValue;
            return true;
        }

        if (value is decimal decimalValue && decimalValue >= int.MinValue
            && decimalValue <= int.MaxValue
            && decimalValue % 1 == 0)
        {
            result = (int)decimalValue;
            return true;
        }

        return false;
    }
}
