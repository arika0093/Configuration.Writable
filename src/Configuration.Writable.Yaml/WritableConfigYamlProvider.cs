using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for Yaml files.
/// </summary>
public record WritableConfigYamlProvider : IWritableConfigProvider
{
    /// <summary>
    /// Gets or sets the serializer used to convert objects to and from a specific format.
    /// </summary>
    public ISerializer Serializer { get; init; } =
        new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public string FileExtension => "yaml";

    /// <inheritdoc />
    public void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddYamlFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var sectionName = options.SectionName;
        var serializer = Serializer;

        string yamlString;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            var wrapper = new Dictionary<string, T> { [sectionName] = config };
            yamlString = serializer.Serialize(wrapper);
        }
        else
        {
            yamlString = serializer.Serialize(config);
        }

        return Encoding.GetBytes(yamlString);
    }
}
