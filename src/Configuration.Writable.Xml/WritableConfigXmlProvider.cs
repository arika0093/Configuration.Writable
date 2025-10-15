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
        where T : class
    {
        var filePath = options.ConfigFilePath;
        if (!FileProvider.FileExists(filePath))
        {
            return Activator.CreateInstance<T>();
        }

        var stream = FileProvider.GetFileStream(filePath);
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
    public override async Task SaveAsync<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var contents = GetSaveContentsCore(config, operations, options);
        await FileProvider
            .SaveToFileAsync(options.ConfigFilePath, contents, cancellationToken, options.Logger)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Core method to get save contents with optional operations.
    /// </summary>
    private static ReadOnlyMemory<byte> GetSaveContentsCore<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options
    )
        where T : class
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

        // Apply deletion operations
        if (operations.HasOperations)
        {
            foreach (var keyToDelete in operations.KeysToDelete)
            {
                DeleteKeyFromXml(configElement, keyToDelete, options);
            }
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

    /// <summary>
    /// Deletes a key from the XML element based on the property path.
    /// </summary>
    /// <param name="element">The XML element to modify.</param>
    /// <param name="keyPath">The property path to delete (e.g., "Parent:Child").</param>
    /// <param name="options">The configuration options.</param>
    private static void DeleteKeyFromXml<T>(
        XmlElement element,
        string keyPath,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var parts = keyPath.Split(':');
        if (parts.Length == 0)
        {
            return;
        }

        // Navigate to the parent element
        XmlElement? current = element;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var child = current.SelectSingleNode(parts[i]) as XmlElement;
            if (child != null)
            {
                current = child;
            }
            else
            {
                // Path doesn't exist, nothing to delete
                options.Logger?.LogDebug(
                    "Key path {KeyPath} not found for deletion, skipping",
                    keyPath
                );
                return;
            }
        }

        // Delete the final element
        var finalKey = parts[^1];
        var targetElement = current.SelectSingleNode(finalKey) as XmlElement;
        if (targetElement != null)
        {
            current.RemoveChild(targetElement);
            options.Logger?.LogDebug("Deleted key {KeyPath} from configuration", keyPath);
        }
        else
        {
            options.Logger?.LogDebug("Key {KeyPath} not found for deletion, skipping", keyPath);
        }
    }
}
