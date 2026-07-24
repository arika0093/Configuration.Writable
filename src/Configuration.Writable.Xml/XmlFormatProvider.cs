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
    private static class SerializerCache<T>
        where T : class, new()
    {
        internal static readonly XmlSerializer Instance = new(typeof(T));
    }

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
        var xmlDoc = await XDocument
            .LoadAsync(stream, LoadOptions.None, cancellationToken)
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
            var serializer = new XmlSerializer(type, new XmlRootAttribute(current.Name.LocalName));
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
        IWritableOptionsConfiguration options,
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
        IWritableOptionsConfiguration options
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
        var serializer = SerializerCache<T>.Instance;
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
        IWritableOptionsConfiguration options
    )
        where T : class, new()
    {
        var parts = options.SectionNameParts;
        var existingDoc = LoadExistingDocument(options);
        var configElement = SerializeConfiguration(config);
        var resultDoc =
            existingDoc?.Root == null
                ? CreatePartialDocument(configElement, parts, options)
                : MergePartialDocument(existingDoc, configElement, parts, options);

        return WriteDocument(resultDoc, options);
    }

    private static XDocument? LoadExistingDocument(IWritableOptionsConfiguration options)
    {
        try
        {
            var pipeReader = options.FileProvider.GetFilePipeReader(options.ConfigFilePath);
            if (pipeReader == null)
            {
                return null;
            }

            using var stream = pipeReader.AsStream(leaveOpen: false);
#if NET8_0_OR_GREATER
            var document = XDocument
                .LoadAsync(stream, LoadOptions.None, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
#else
            var document = XDocument.Load(stream);
#endif
            options.Logger?.ZLogTrace($"Loaded existing XML file for partial update");
            return document;
        }
        catch (XmlException ex)
        {
            options.Logger?.ZLogWarning(
                ex,
                $"Failed to parse existing XML file, will create new file structure"
            );
            return null;
        }
    }

    private static XmlElement SerializeConfiguration<T>(T config)
        where T : class, new()
    {
        using var writer = new StringWriter();
        SerializerCache<T>.Instance.Serialize(writer, config);
        var document = new XmlDocument();
        document.LoadXml(writer.ToString());
        return document.DocumentElement
            ?? throw new InvalidOperationException("Failed to serialize configuration to XML");
    }

    private static XDocument CreatePartialDocument(
        XmlElement configElement,
        System.Collections.Generic.IReadOnlyList<string> parts,
        IWritableOptionsConfiguration options
    )
    {
        options.Logger?.ZLogTrace(
            $"Creating new nested section structure for section: {string.Join(":", parts)}"
        );

        var innerXml = configElement.InnerXml;
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            innerXml = $"<{parts[i]}>{innerXml}</{parts[i]}>";
        }

        return XDocument.Parse(
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>{innerXml}</configuration>
            """
        );
    }

    private static XDocument MergePartialDocument(
        XDocument document,
        XmlElement configElement,
        System.Collections.Generic.IReadOnlyList<string> parts,
        IWritableOptionsConfiguration options
    )
    {
        options.Logger?.ZLogTrace(
            $"Merging with existing XML file for section: {string.Join(":", parts)}"
        );

        var root =
            document.Root
            ?? throw new InvalidOperationException("Existing XML document has no root element");
        var parent = GetSectionParent(root, parts);
        var sectionName = parts[parts.Count - 1];
        var replacement = new XElement(sectionName, XElement.Parse(configElement.OuterXml).Nodes());
        var existing = parent.Element(sectionName);

        if (existing == null)
        {
            parent.Add(replacement);
        }
        else
        {
            existing.ReplaceWith(replacement);
        }

        return document;
    }

    private static XElement GetSectionParent(
        XElement root,
        System.Collections.Generic.IReadOnlyList<string> parts
    )
    {
        var current = root;
        for (int i = 0; i < parts.Count - 1; i++)
        {
            var existing = current.Element(parts[i]);
            if (existing == null)
            {
                existing = new XElement(parts[i]);
                current.Add(existing);
            }

            current = existing;
        }

        return current;
    }

    private static ReadOnlyMemory<byte> WriteDocument(
        XDocument document,
        IWritableOptionsConfiguration options
    )
    {
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
        document.WriteTo(xmlWriter);
        xmlWriter.Flush();

        options.Logger?.ZLogTrace($"Partial XML serialization completed successfully");

        return Encoding.UTF8.GetBytes(resultWriter.ToString());
    }
}
