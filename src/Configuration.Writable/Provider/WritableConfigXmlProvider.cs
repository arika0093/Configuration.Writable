using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable.Provider;

/// <summary>
/// Writable configuration implementation for XML files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public class WritableConfigXmlProvider<T> : IWritableConfigProvider<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public string FileExtension => "xml";

    /// <inheritdoc />
    public void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddXmlFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetSaveContents(T config, WritableConfigurationOptions<T> options)
    {
        var sectionName = options.SectionName;
        var serializer = new XmlSerializer(
            typeof(T),
            new XmlRootAttribute(
                string.IsNullOrWhiteSpace(sectionName) ? typeof(T).Name : sectionName
            )
        );
        using var ms = new MemoryStream();
        serializer.Serialize(ms, config);
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }
}
