using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;

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
    {
        var filePath = options.ConfigFilePath;
        if (!FileProvider.FileExists(filePath))
        {
            return new T();
        }

        var stream = FileProvider.GetFileStream(filePath);
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
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
    {
        var xmlDoc = XDocument.Load(stream);
        var root = xmlDoc.Root;

        if (root == null)
        {
            return new T();
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
                    return new T();
                }
            }

            // Deserialize from the found section
            using var reader = current.CreateReader();
            var serializer = new XmlSerializer(
                typeof(T),
                new XmlRootAttribute(current.Name.LocalName)
            );
            return (serializer.Deserialize(reader) as T) ?? new T();
        }

        // Deserialize from root
        using var rootReader = root.CreateReader();
        var rootSerializer = new XmlSerializer(
            typeof(T),
            new XmlRootAttribute(root.Name.LocalName)
        );
        return (rootSerializer.Deserialize(rootReader) as T) ?? new T();
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
    {
        var contents = GetSaveContents(config, options);
        await FileProvider
            .SaveToFileAsync(options.ConfigFilePath, contents, options.Logger, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the save contents for the configuration.
    /// </summary>
    private static ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class, new()
    {
        var sectionName = options.SectionName;
        var parts = GetSplitedSections(sectionName);

        // Serialize the configuration to XML
        var serializer = new XmlSerializer(typeof(T));
        using var sw = new StringWriter();
        serializer.Serialize(sw, config);
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(sw.ToString());

        // Get the root element containing the serialized data
        var configElement = xmlDocument.DocumentElement;
        if (configElement == null)
        {
            throw new InvalidOperationException("Failed to serialize configuration to XML");
        }

        // Build nested XML structure
        var innerXml = configElement.InnerXml;

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
