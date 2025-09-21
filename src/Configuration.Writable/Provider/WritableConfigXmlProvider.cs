using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for XML files.
/// </summary>
internal record WritableConfigXmlProvider : IWritableConfigProvider
{
    /// <inheritdoc />
    public string FileExtension => "xml";

    /// <inheritdoc />
    public void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddXmlFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        throw new NotImplementedException("XML writable configuration is not implemented yet.");
        var sectionName = options.SectionName;
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            // save to <configuration>...</configuration>
            var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute("configuration"));
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            using var ms = new MemoryStream();
            serializer.Serialize(ms, config, ns);
            return new ReadOnlyMemory<byte>(ms.ToArray());
        }
        else
        {
            // save to <configuration><{sectionName}>...</{sectionName}></configuration>
            var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(sectionName));
            using var sw = new StringWriter();
            serializer.Serialize(sw, config);
            // wrap with <configuration>...</configuration>
            var xmlString = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    {sw}
                </configuration>
                """;
            return Encoding.UTF8.GetBytes(xmlString);
        }
    }
}
