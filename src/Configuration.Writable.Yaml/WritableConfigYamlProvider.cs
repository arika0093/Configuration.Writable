using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Configuration.Writable.Provider;

/// <summary>
/// Writable configuration implementation for Yaml files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public class WritableConfigYamlProvider<T> : IWritableConfigProvider<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddYamlFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetSaveContents(T config, WritableConfigurationOptions<T> options)
    {
        var sectionName = options.SectionName;
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

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
