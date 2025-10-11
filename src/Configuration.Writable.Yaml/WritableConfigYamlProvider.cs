using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        if (!FileWriter.FileExists(filePath))
        {
            return Activator.CreateInstance<T>();
        }

        var stream = FileWriter.GetFileStream(filePath);
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
                    var key = stringKeyDict.Keys.FirstOrDefault(k => string.Equals(k, section, StringComparison.OrdinalIgnoreCase));
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
                    var key = objectKeyDict.Keys
                        .OfType<string>()
                        .FirstOrDefault(k => string.Equals(k, section, StringComparison.OrdinalIgnoreCase));
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
    public override ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var sectionName = options.SectionName;
        var serializer = Serializer;

        // Use the new nested section creation method
        var nestedSection = CreateNestedSection(sectionName, config);
        var yamlString = serializer.Serialize(nestedSection);
        return Encoding.GetBytes(yamlString);
    }
}
