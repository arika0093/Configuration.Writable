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
        new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

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
    public override T LoadConfiguration<T>(Stream stream, WritableOptionsConfiguration<T> options)
    {
        using var reader = new StreamReader(stream, Encoding);
        var yamlContent = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return new T();
        }

        // Deserialize the YAML to a dictionary first
        var deserializer = Deserializer;
        var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        if (yamlObject == null)
        {
            return new T();
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
                        return new T();
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
                        return new T();
                    }
                }
                else
                {
                    // Current is not a dictionary, return default instance
                    return new T();
                }
            }

            // Serialize and deserialize to convert to T
            var serializer = Serializer;
            var serialized = serializer.Serialize(current);
            return deserializer.Deserialize<T>(serialized) ?? new T();
        }

        // Deserialize from root
        var rootSerialized = Serializer.Serialize(yamlObject);
        return deserializer.Deserialize<T>(rootSerialized) ?? new T();
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
        var sectionName = options.SectionName;
        var serializer = Serializer;

        // Serialize config to dictionary
        var configYaml = serializer.Serialize(config);
        var deserializer = Deserializer;
        var configDict =
            deserializer.Deserialize<Dictionary<string, object>>(configYaml)
            ?? new Dictionary<string, object>();

        // Create nested section structure
        var nestedSection = CreateNestedSection(sectionName, configDict);
        var yamlString = serializer.Serialize(nestedSection);
        return Encoding.GetBytes(yamlString);
    }
}
