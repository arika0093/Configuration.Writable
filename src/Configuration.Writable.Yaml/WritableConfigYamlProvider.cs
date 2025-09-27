using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
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
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public override string FileExtension => "yaml";

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddYamlFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, Stream stream) =>
        configuration.AddYamlStream(stream);

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
