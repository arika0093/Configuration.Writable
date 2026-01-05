using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Writable configuration implementation for XML files.
/// </summary>
public class XmlFormatProvider : FormatProviderBase
{
    /// <inheritdoc />
    public override string FileExtension => "xml";

    /// <inheritdoc />
    public override async ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        System.Collections.Generic.List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
    {
        // Use PipeReader.AsStream for compatibility with XDocument.Load
        // The stream owns the PipeReader when leaveOpen is false
        using var stream = reader.AsStream(leaveOpen: false);
#if NET8_0_OR_GREATER
        var xmlDoc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken)
            .ConfigureAwait(false);
#else
        var xmlDoc = XDocument.Load(stream);
#endif
        var root = xmlDoc.Root;

        if (root == null)
        {
            return Activator.CreateInstance(type)!;
        }

        // Navigate to the section if specified
        if (sectionNameParts.Count > 0)
        {
            var current = root;

            foreach (var section in sectionNameParts)
            {
                var element = current.Element(section);
                if (element != null)
                {
                    current = element;
                }
                else
                {
                    // Section not found, return default instance
                    return Activator.CreateInstance(type)!;
                }
            }

            using var xmlReader = current.CreateReader();
            var serializer = new XmlSerializer(
                type,
                new XmlRootAttribute(current.Name.LocalName)
            );
            return serializer.Deserialize(xmlReader) ?? Activator.CreateInstance(type)!;
        }

        using (var xmlReader = root.CreateReader())
        {
            var serializer = new XmlSerializer(type, new XmlRootAttribute(root.Name.LocalName));
            return serializer.Deserialize(xmlReader) ?? Activator.CreateInstance(type)!;
        }
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken = default
    )
    {
        var contents = GetSaveContents(config, options);
        await options
            .FileProvider.SaveToFileAsync(
                options.ConfigFilePath,
                contents,
                options.Logger,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the save contents for the configuration.
    /// </summary>
    private static ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var parts = options.SectionNameParts;

        // Section name specified - use partial write (merge with existing file)
        if (parts.Count > 0)
        {
            return GetPartialSaveContents(config, options);
        }

        // No section name - create full XML with <configuration> wrapper
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

        // Build nested XML structure with innerXml
        var innerXml = configElement.InnerXml;

        var xmlString = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>{innerXml}</configuration>
            """;
        return Encoding.UTF8.GetBytes(xmlString);
    }

    /// <summary>
    /// Gets the save contents for partial write (when SectionName is specified).
    /// Reads existing file and merges the new configuration into the specified section.
    /// </summary>
    private static ReadOnlyMemory<byte> GetPartialSaveContents<T>(
        T config,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var parts = options.SectionNameParts;
        XDocument? existingDoc = null;

        // Try to read existing file using PipeReader
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            try
            {
                var pipeReader = options.FileProvider.GetFilePipeReader(options.ConfigFilePath);
                if (pipeReader != null)
                {
                    using var stream = pipeReader.AsStream(leaveOpen: false);
#if NET8_0_OR_GREATER
                    existingDoc = XDocument.LoadAsync(
                            stream,
                            LoadOptions.None,
                            CancellationToken.None
                        )
                        .GetAwaiter()
                        .GetResult();
#else
                    existingDoc = XDocument.Load(stream);
#endif
                    options.Logger?.ZLogTrace($"Loaded existing XML file for partial update");
                }
            }
            catch (XmlException ex)
            {
                options.Logger?.ZLogWarning(
                    ex,
                    $"Failed to parse existing XML file, will create new file structure"
                );
            }
        }

        // Serialize the configuration to XML
        var serializer = new XmlSerializer(typeof(T));
        using var sw = new StringWriter();
        serializer.Serialize(sw, config);
        var configXmlDoc = new XmlDocument();
        configXmlDoc.LoadXml(sw.ToString());

        // Get the root element containing the serialized data
        var configElement = configXmlDoc.DocumentElement;
        if (configElement == null)
        {
            throw new InvalidOperationException("Failed to serialize configuration to XML");
        }

        XDocument resultDoc;

        if (existingDoc == null || existingDoc.Root == null)
        {
            // No existing file, create new nested structure
            options.Logger?.ZLogTrace(
                $"Creating new nested section structure for section: {string.Join(":", parts)}"
            );

            // Build nested XML structure
            var innerXml = configElement.InnerXml;

            // Build the nested structure from innermost to outermost
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                innerXml = $"<{parts[i]}>{innerXml}</{parts[i]}>";
            }

            var xmlString = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>{innerXml}</configuration>
                """;
            resultDoc = XDocument.Parse(xmlString);
        }
        else
        {
            // Merge with existing document
            options.Logger?.ZLogTrace(
                $"Merging with existing XML file for section: {string.Join(":", parts)}"
            );

            resultDoc = new XDocument(existingDoc);
            var root = resultDoc.Root;

            if (root == null)
            {
                throw new InvalidOperationException("Existing XML document has no root element");
            }

            // Navigate to the parent element where we need to insert/update the section
            XElement? current = root;

            for (int i = 0; i < parts.Count; i++)
            {
                var sectionName = parts[i];
                var existing = current.Element(sectionName);

                if (i == parts.Count - 1)
                {
                    // This is the final section - replace or add it
                    if (existing != null)
                    {
                        // Replace existing section
                        existing.ReplaceWith(
                            new XElement(
                                sectionName,
                                XElement.Parse(configElement.OuterXml).Nodes()
                            )
                        );
                    }
                    else
                    {
                        // Add new section
                        current.Add(
                            new XElement(
                                sectionName,
                                XElement.Parse(configElement.OuterXml).Nodes()
                            )
                        );
                    }
                }
                else
                {
                    // Navigate deeper or create intermediate sections
                    if (existing != null)
                    {
                        current = existing;
                    }
                    else
                    {
                        // Create missing intermediate section
                        var newSection = new XElement(sectionName);
                        current.Add(newSection);
                        current = newSection;
                    }
                }
            }
        }

        // Write result to string
        using var resultWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(
            resultWriter,
            new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false,
            }
        );
        resultDoc.WriteTo(xmlWriter);
        xmlWriter.Flush();

        options.Logger?.ZLogTrace($"Partial XML serialization completed successfully");

        return Encoding.UTF8.GetBytes(resultWriter.ToString());
    }
}
