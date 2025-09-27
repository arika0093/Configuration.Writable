using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for XML files.
/// </summary>
public class WritableConfigXmlProvider : WritableConfigProviderBase
{
    /// <inheritdoc />
    public override string FileExtension => "xml";

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddXmlFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, Stream stream) =>
        configuration.AddXmlStream(stream);

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
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
            // Split section name by ':' or '__' and create nested XML structure
            var parts = sectionName.Split(
                new string[] { ":", "__" },
                StringSplitOptions.RemoveEmptyEntries
            );

            // first serialize to <AnyName>...</AnyName>
            var serializer = new XmlSerializer(typeof(T));
            using var sw = new StringWriter();
            serializer.Serialize(sw, config);
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(sw.ToString());

            // Build nested XML structure
            var innerXml = xmlDocument.DocumentElement?.InnerXml ?? "";

            // Build the nested structure from innermost to outermost
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                innerXml = $"<{parts[i]}>{innerXml}</{parts[i]}>";
            }

            var xmlString = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>{innerXml}</configuration>
                """;
            return Encoding.UTF8.GetBytes(xmlString);
        }
    }
}
