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
using Configuration.Writable.FormatProvider;
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
                FormatProviderBase.ExecuteWithBackupRecovery(
                    readOptions,
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
        return FormatProviderBase.ExecuteWithBackupRecovery(
            options,
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
        var serializer = XmlFormatProvider.SerializerCache<T>.Instance;
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
        XmlFormatProvider.AddSchemaMetadata(
            configElement,
            options.SchemaMetadata,
            _schemaVersionProperty
        );

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
        var configElement = XmlFormatProvider.SerializeConfiguration(config);
        XmlFormatProvider.AddSchemaMetadata(
            configElement,
            options.SchemaMetadata,
            _schemaVersionProperty
        );
        var resultDoc =
            existingDoc?.Root == null
                ? XmlFormatProvider.CreatePartialDocument(configElement, parts, options)
                : XmlFormatProvider.MergePartialDocument(
                    existingDoc,
                    configElement,
                    parts,
                    options
                );

        return XmlFormatProvider.WriteDocument(resultDoc, options);
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
}
