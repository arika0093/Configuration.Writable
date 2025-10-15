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

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for Yaml files.
/// </summary>
public class WritableConfigYamlProvider : WritableConfigProviderBase
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
        new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public override string FileExtension => "yaml";

    /// <inheritdoc />
    public override T LoadConfiguration<T>(WritableConfigurationOptions<T> options)
        where T : class
    {
        var filePath = options.ConfigFilePath;
        if (!FileProvider.FileExists(filePath))
        {
            return Activator.CreateInstance<T>();
        }

        var stream = FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return Activator.CreateInstance<T>();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
        where T : class
    {
        using var reader = new StreamReader(stream, Encoding);
        var yamlContent = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Activator.CreateInstance<T>();
        }

        // Deserialize the YAML to a dictionary first
        var deserializer = Deserializer;
        var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        if (yamlObject == null)
        {
            return Activator.CreateInstance<T>();
        }

        // Navigate to the section if specified
        var sectionName = options.SectionName;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            var sections = GetSplitedSections(sectionName);
            object current = yamlObject;

            foreach (var section in sections)
            {
                // YamlDotNet can deserialize as Dictionary<string, object> at the root level
                // but nested dictionaries might be Dictionary<object, object>
                if (current is Dictionary<string, object> stringKeyDict)
                {
                    // Try case-insensitive lookup to handle naming convention differences
                    var key = stringKeyDict.Keys.FirstOrDefault(k =>
                        string.Equals(k, section, StringComparison.OrdinalIgnoreCase)
                    );
                    if (key != null && stringKeyDict.TryGetValue(key, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        // Section not found, return default instance
                        return Activator.CreateInstance<T>();
                    }
                }
                else if (current is Dictionary<object, object> objectKeyDict)
                {
                    // Handle Dictionary<object, object> for nested sections
                    var key = objectKeyDict
                        .Keys.OfType<string>()
                        .FirstOrDefault(k =>
                            string.Equals(k, section, StringComparison.OrdinalIgnoreCase)
                        );
                    if (key != null && objectKeyDict.TryGetValue(key, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        // Section not found, return default instance
                        return Activator.CreateInstance<T>();
                    }
                }
                else
                {
                    // Current is not a dictionary, return default instance
                    return Activator.CreateInstance<T>();
                }
            }

            // Serialize and deserialize to convert to T
            var serializer = Serializer;
            var serialized = serializer.Serialize(current);
            return deserializer.Deserialize<T>(serialized) ?? Activator.CreateInstance<T>();
        }

        // Deserialize from root
        var rootSerialized = Serializer.Serialize(yamlObject);
        return deserializer.Deserialize<T>(rootSerialized) ?? Activator.CreateInstance<T>();
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var contents = GetSaveContentsCore(config, operations, options);
        await FileProvider
            .SaveToFileAsync(options.ConfigFilePath, contents, cancellationToken, options.Logger)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Core method to get save contents with optional operations.
    /// </summary>
    private ReadOnlyMemory<byte> GetSaveContentsCore<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var sectionName = options.SectionName;
        var serializer = Serializer;

        // Serialize config to dictionary
        var configYaml = serializer.Serialize(config);
        var deserializer = Deserializer;
        var configDict =
            deserializer.Deserialize<Dictionary<string, object>>(configYaml)
            ?? new Dictionary<string, object>();

        // Apply deletion operations
        if (operations.HasOperations)
        {
            foreach (var keyToDelete in operations.KeysToDelete)
            {
                DeleteKeyFromDict(configDict, keyToDelete, options);
            }
        }

        // Create nested section structure
        var nestedSection = CreateNestedSection(sectionName, configDict);
        var yamlString = serializer.Serialize(nestedSection);
        return Encoding.GetBytes(yamlString);
    }

    /// <summary>
    /// Deletes a key from the dictionary based on the property path.
    /// </summary>
    /// <param name="dict">The dictionary to modify.</param>
    /// <param name="keyPath">The property path to delete (e.g., "Parent:Child").</param>
    /// <param name="options">The configuration options.</param>
    private static void DeleteKeyFromDict<T>(
        Dictionary<string, object> dict,
        string keyPath,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var parts = keyPath.Split(':');
        if (parts.Length == 0)
        {
            return;
        }

        // Navigate to the parent dictionary
        object current = dict;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current is Dictionary<string, object> stringKeyDict)
            {
                var key = stringKeyDict.Keys.FirstOrDefault(k =>
                    string.Equals(k, parts[i], StringComparison.OrdinalIgnoreCase)
                );
                if (key != null && stringKeyDict.TryGetValue(key, out var value))
                {
                    current = value;
                }
                else
                {
                    // Path doesn't exist, nothing to delete
                    options.Logger?.LogDebug(
                        "Key path {KeyPath} not found for deletion, skipping",
                        keyPath
                    );
                    return;
                }
            }
            else if (current is Dictionary<object, object> objectKeyDict)
            {
                var key = objectKeyDict
                    .Keys.OfType<string>()
                    .FirstOrDefault(k =>
                        string.Equals(k, parts[i], StringComparison.OrdinalIgnoreCase)
                    );
                if (key != null && objectKeyDict.TryGetValue(key, out var value))
                {
                    current = value;
                }
                else
                {
                    // Path doesn't exist, nothing to delete
                    options.Logger?.LogDebug(
                        "Key path {KeyPath} not found for deletion, skipping",
                        keyPath
                    );
                    return;
                }
            }
            else
            {
                // Current is not a dictionary, nothing to delete
                options.Logger?.LogDebug(
                    "Key path {KeyPath} not found for deletion, skipping",
                    keyPath
                );
                return;
            }
        }

        // Delete the final key
        var finalKey = parts[^1];
        if (current is Dictionary<string, object> finalStringDict)
        {
            var key = finalStringDict.Keys.FirstOrDefault(k =>
                string.Equals(k, finalKey, StringComparison.OrdinalIgnoreCase)
            );
            if (key != null && finalStringDict.Remove(key))
            {
                options.Logger?.LogDebug("Deleted key {KeyPath} from configuration", keyPath);
            }
            else
            {
                options.Logger?.LogDebug("Key {KeyPath} not found for deletion, skipping", keyPath);
            }
        }
        else if (current is Dictionary<object, object> finalObjectDict)
        {
            var key = finalObjectDict
                .Keys.OfType<string>()
                .FirstOrDefault(k =>
                    string.Equals(k, finalKey, StringComparison.OrdinalIgnoreCase)
                );
            if (key != null && finalObjectDict.Remove(key))
            {
                options.Logger?.LogDebug("Deleted key {KeyPath} from configuration", keyPath);
            }
            else
            {
                options.Logger?.LogDebug("Key {KeyPath} not found for deletion, skipping", keyPath);
            }
        }
        else
        {
            options.Logger?.LogDebug("Key {KeyPath} not found for deletion, skipping", keyPath);
        }
    }
}
