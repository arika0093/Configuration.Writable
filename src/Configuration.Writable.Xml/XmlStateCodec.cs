using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Configuration.Writable.Migration;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.State;

/// <summary>
/// Native XML state codec. Replaces the <c>XmlFormatProvider</c> pipeline with
/// direct <see cref="IStateCodec{T}"/> serialization over
/// <see cref="FileStateResource{T}"/> file operations.
/// </summary>
internal sealed class XmlStateCodec<T> : IStateCodec<T>
    where T : class, new()
{
    private readonly string _schemaVersionProperty;
    private readonly IReadOnlyList<string> _schemaVersionFallbackProperties;

    internal XmlStateCodec(
        string schemaVersionProperty,
        IReadOnlyList<string> schemaVersionFallbackProperties
    )
    {
        _schemaVersionProperty = schemaVersionProperty;
        _schemaVersionFallbackProperties = schemaVersionFallbackProperties;
    }

    public ValueTask<T> ReadAsync(
        IStateResource resource,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileResource = GetFileResource(resource);
        var options = fileResource.Options;
        var readPath =
            options.PromoteSaveLocationEnabled && fileResource.FileExists(options.ConfigFilePath)
                ? options.ConfigFilePath
                : options.ReadFilePath;
        var readOptions = options with { ConfigFilePath = readPath };
        var result = MigrationLoaderExtension.LoadWithMigration(
            readOptions,
            type => LoadAsType(fileResource, readPath, type, readOptions),
            () =>
                FileBackupRecovery.Execute(
                    readOptions.FileBackend,
                    readPath,
                    readOptions.Logger,
                    () => ReadSchemaMetadata(fileResource, readPath, readOptions)
                ),
            static _ => { }
        );
        return new ValueTask<T>(result);
    }

    public async ValueTask WriteAsync(
        T value,
        IStateResource resource,
        CancellationToken cancellationToken = default
    )
    {
        var fileResource = GetFileResource(resource);
        var options = fileResource.Options;
        var contents = GetSaveContents(value, fileResource, options);
        await fileResource
            .WriteAsync(options.ConfigFilePath, contents, cancellationToken)
            .ConfigureAwait(false);
    }

    private static object LoadAsType(
        FileStateResource<T> resource,
        string path,
        Type type,
        WritableOptionsConfiguration<T> options
    )
    {
        return FileBackupRecovery.Execute(
            options.FileBackend,
            path,
            options.Logger,
            () => LoadCore(resource, path, type, options)
        );
    }

    private static object LoadCore(
        FileStateResource<T> resource,
        string path,
        Type type,
        WritableOptionsConfiguration<T> options
    )
    {
        if (!resource.FileExists(path))
        {
            return Activator.CreateInstance(type)!;
        }

        using var stream = resource.OpenRead(path);
        var xmlDoc = XDocument.Load(stream);
        var root = xmlDoc.Root;

        if (root == null)
        {
            return Activator.CreateInstance(type)!;
        }

        // Navigate to the section if specified
        if (options.SectionNameParts.Count > 0)
        {
            var current = root;

            foreach (var section in options.SectionNameParts)
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

    private OptionsSchemaMetadata? ReadSchemaMetadata(
        FileStateResource<T> resource,
        string path,
        WritableOptionsConfiguration<T> options
    )
    {
        if (!resource.FileExists(path))
        {
            return null;
        }

        using var stream = resource.OpenRead(path);
        var document = XDocument.Load(stream);
        var current = document.Root;
        if (current == null)
        {
            return null;
        }

        foreach (var section in options.SectionNameParts)
        {
            current = current.Element(section);
            if (current == null)
            {
                return null;
            }
        }

        var versionElement = new[] { _schemaVersionProperty }
            .Concat(_schemaVersionFallbackProperties)
            .Distinct(StringComparer.Ordinal)
            .Select(name => current.Element(name))
            .FirstOrDefault(element => element != null);
        var version = 1;
        if (versionElement != null)
        {
            if (!int.TryParse(versionElement.Value, out var parsedVersion))
            {
                throw new FormatException(
                    $"XML metadata element '{versionElement.Name}' must be an integer."
                );
            }

            version = parsedVersion;
        }

        return new OptionsSchemaMetadata(null, version);
    }

    private ReadOnlyMemory<byte> GetSaveContents(
        T config,
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    )
    {
        var parts = options.SectionNameParts;

        // Section name specified - use partial write (merge with existing file)
        if (parts.Count > 0)
        {
            return GetPartialSaveContents(config, resource, options);
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
        AddSchemaMetadata(configElement, options.SchemaMetadata, _schemaVersionProperty);

        // Build nested XML structure with innerXml
        var innerXml = configElement.InnerXml;

        var xmlString = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>{innerXml}</configuration>
            """;
        return Encoding.UTF8.GetBytes(xmlString);
    }

    private ReadOnlyMemory<byte> GetPartialSaveContents(
        T config,
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    )
    {
        var parts = options.SectionNameParts;
        var existingDoc = LoadExistingDocument(resource, options);
        var configElement = SerializeConfiguration(config);
        AddSchemaMetadata(configElement, options.SchemaMetadata, _schemaVersionProperty);
        var resultDoc =
            existingDoc?.Root == null
                ? CreatePartialDocument(configElement, parts, options)
                : MergePartialDocument(existingDoc, configElement, parts, options);

        return WriteDocument(resultDoc, options);
    }

    private static XDocument? LoadExistingDocument(
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    )
    {
        try
        {
            if (!resource.FileExists(options.ConfigFilePath))
            {
                return null;
            }

            using var stream = resource.OpenRead(options.ConfigFilePath);
            var document = XDocument.Load(stream);
            options.Logger?.LogTrace("Loaded existing XML file for partial update");
            return document;
        }
        catch (XmlException ex)
        {
            options.Logger?.LogWarning(
                ex,
                "Failed to parse existing XML file, will create new file structure"
            );
            return null;
        }
    }

    private static FileStateResource<T> GetFileResource(IStateResource resource) =>
        resource as FileStateResource<T>
        ?? throw new ArgumentException(
            "The XML state codec requires a file state resource.",
            nameof(resource)
        );

    private static class SerializerCache<TSerializer>
        where TSerializer : class, new()
    {
        internal static readonly XmlSerializer Instance = new(typeof(TSerializer));
    }

    private static XmlElement SerializeConfiguration<TConfig>(TConfig config)
        where TConfig : class, new()
    {
        using var writer = new StringWriter();
        SerializerCache<TConfig>.Instance.Serialize(writer, config);
        var document = new XmlDocument();
        document.LoadXml(writer.ToString());
        return document.DocumentElement
            ?? throw new InvalidOperationException("Failed to serialize configuration to XML");
    }

    private static void AddSchemaMetadata(
        XmlElement configElement,
        OptionsSchemaMetadata? metadata,
        string schemaVersionProperty
    )
    {
        if (metadata == null)
        {
            return;
        }

        var document = configElement.OwnerDocument;
        if (metadata.Version is not null)
        {
            var version = document.CreateElement(schemaVersionProperty);
            version.InnerText = metadata.Version.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
            configElement.PrependChild(version);
        }
    }

    private static XDocument CreatePartialDocument(
        XmlElement configElement,
        System.Collections.Generic.IReadOnlyList<string> parts,
        WritableOptionsConfiguration<T> options
    )
    {
        options.Logger?.LogTrace(
            "Creating new nested section structure for section: {Section}",
            string.Join(":", parts)
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
        WritableOptionsConfiguration<T> options
    )
    {
        options.Logger?.LogTrace(
            "Merging with existing XML file for section: {Section}",
            string.Join(":", parts)
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
        WritableOptionsConfiguration<T> options
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

        options.Logger?.LogTrace("Partial XML serialization completed successfully");

        return Encoding.UTF8.GetBytes(resultWriter.ToString());
    }
}
