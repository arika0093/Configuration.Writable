using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for XML files.
/// </summary>
public class WritableConfigXmlProvider : WritableConfigProviderBase
{
    /// <inheritdoc />
    public override string FileExtension => "xml";

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
        var xmlDoc = XDocument.Load(stream);
        var root = xmlDoc.Root;

        if (root == null)
        {
            return Activator.CreateInstance<T>();
        }

        // Navigate to the section if specified
        var sectionName = options.SectionName;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            var sections = GetSplitedSections(sectionName);
            var current = root;

            foreach (var section in sections)
            {
                var element = current.Element(section);
                if (element != null)
                {
                    current = element;
                }
                else
                {
                    // Section not found, return default instance
                    return Activator.CreateInstance<T>();
                }
            }

            // Deserialize from the found section
            using var reader = current.CreateReader();
            var serializer = new XmlSerializer(
                typeof(T),
                new XmlRootAttribute(current.Name.LocalName)
            );
            return (serializer.Deserialize(reader) as T) ?? Activator.CreateInstance<T>();
        }

        // Deserialize from root
        using var rootReader = root.CreateReader();
        var rootSerializer = new XmlSerializer(
            typeof(T),
            new XmlRootAttribute(root.Name.LocalName)
        );
        return (rootSerializer.Deserialize(rootReader) as T) ?? Activator.CreateInstance<T>();
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var sectionName = options.SectionName;
        // Split section name by ':' or '__' and create nested XML structure
        var parts = GetSplitedSections(sectionName);

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
