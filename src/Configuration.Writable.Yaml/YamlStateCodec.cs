using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Migration;
using Microsoft.Extensions.Logging;
using VYaml.Serialization;

namespace Configuration.Writable.State;

/// <summary>
/// Native YAML state codec. Replaces the <c>YamlFormatProvider</c> pipeline with
/// direct <see cref="IStateCodec{T}"/> serialization over
/// <see cref="FileStateResource{T}"/> file operations.
/// </summary>
internal sealed class YamlStateCodec<T> : IStateCodec<T>
    where T : class, new()
{
    private readonly YamlSerializerOptions _serializerOptions;
    private readonly Encoding _encoding;
    private readonly string _schemaVersionProperty;
    private readonly IReadOnlyList<string> _schemaVersionFallbackProperties;

    internal YamlStateCodec(
        YamlSerializerOptions serializerOptions,
        Encoding encoding,
        string schemaVersionProperty,
        IReadOnlyList<string> schemaVersionFallbackProperties
    )
    {
        _serializerOptions =
            serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
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

    private object LoadAsType(
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

    private object LoadCore(
        FileStateResource<T> resource,
        string path,
        Type type,
        WritableOptionsConfiguration<T> options
    )
    {
        if (!resource.FileExists(path))
        {
            return CreateDefault(type);
        }

        var yamlBytes = ReadYamlBytes(resource, path);
        if (YamlCodecSupport.IsEmptyOrWhiteSpace(yamlBytes.Span))
        {
            return CreateDefault(type);
        }

        try
        {
            var targetBytes = YamlCodecSupport.GetSectionBytes(yamlBytes, options.SectionNameParts);
            return targetBytes == null
                ? CreateDefault(type)
                : YamlCodecSupport.Deserialize(type, targetBytes.Value, _serializerOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FormatException("Failed to deserialize YAML configuration.", ex);
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "Non-AOT callers retain the existing runtime type activation fallback; NativeAOT callers register source-generated deserializers."
    )]
    private static object CreateDefault(Type type) => Activator.CreateInstance(type)!;

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

        var yamlBytes = ReadYamlBytes(resource, path);
        if (YamlCodecSupport.IsEmptyOrWhiteSpace(yamlBytes.Span))
        {
            return null;
        }

        YamlCodecSupport.IYamlValue? data;
        try
        {
            data = YamlCodecSupport.ParseYaml(yamlBytes);
        }
        catch (Exception ex)
        {
            throw new FormatException("Failed to read YAML schema metadata.", ex);
        }
        if (data == null)
        {
            return null;
        }

        var current = data;
        foreach (var section in options.SectionNameParts)
        {
            if (
                current is not YamlCodecSupport.YamlMapping mapping
                || !mapping.Values.TryGetValue(section, out current)
            )
            {
                return null;
            }
        }

        if (current is not YamlCodecSupport.YamlMapping metadata)
        {
            throw new FormatException("Options schema metadata must be stored in a YAML mapping.");
        }

        var version =
            YamlCodecSupport.ReadOptionalVersion(
                metadata,
                _schemaVersionProperty,
                _schemaVersionFallbackProperties
            ) ?? 1;
        return new OptionsSchemaMetadata(null, version);
    }

    private ReadOnlyMemory<byte> ReadYamlBytes(FileStateResource<T> resource, string path)
    {
        using var stream = resource.OpenRead(path);
        return ReadYamlBytes(stream);
    }

    private ReadOnlyMemory<byte> ReadYamlBytes(Stream stream)
    {
        if (_encoding.CodePage == Encoding.UTF8.CodePage)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer, 81920);
            var yamlBytes = buffer.ToArray();
            if (!YamlCodecSupport.HasNonUtf8Bom(yamlBytes))
            {
                return yamlBytes;
            }

            using var encodedStream = new MemoryStream(yamlBytes, writable: false);
            return ReadEncodedYamlBytes(encodedStream);
        }

        return ReadEncodedYamlBytes(stream);
    }

    private ReadOnlyMemory<byte> ReadEncodedYamlBytes(Stream stream)
    {
#if NETSTANDARD2_0
        using var streamReader = new StreamReader(stream, _encoding);
#else
        using var streamReader = new StreamReader(
            stream,
            _encoding,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false
        );
#endif

        var yamlContent = streamReader.ReadToEnd();
        return Encoding.UTF8.GetBytes(yamlContent);
    }

    private ReadOnlyMemory<byte> GetSaveContents(
        T config,
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    )
    {
        var sections = options.SectionNameParts;

        if (sections.Count == 0)
        {
            var contents =
                options.SchemaMetadata == null
                    ? SerializeForFile(
                        config,
                        JsonSchemaGeneration.ResolveSchemaReference(
                            options.SchemaBaseUri,
                            options.SchemaMetadata
                        )
                    )
                    : SerializeForFile(
                        (YamlCodecSupport.IYamlValue)
                            YamlCodecSupport.CreateSchemaMetadataDictionary(
                                config,
                                options.SchemaMetadata,
                                _serializerOptions,
                                _schemaVersionProperty
                            ),
                        JsonSchemaGeneration.ResolveSchemaReference(
                            options.SchemaBaseUri,
                            options.SchemaMetadata
                        )
                    );
            return contents;
        }

        // Section specified - use partial write (merge with existing file)
        return GetPartialSaveContents(config, resource, options);
    }

    private ReadOnlyMemory<byte> GetPartialSaveContents(
        T config,
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    )
    {
        var sections = options.SectionNameParts;
        YamlCodecSupport.YamlMapping? existingDocument = null;

        // A malformed existing file must never be replaced with a new partial document.
        // Propagating the parse error preserves the original file for recovery.
        if (resource.FileExists(options.ConfigFilePath))
        {
            using var stream = resource.OpenRead(options.ConfigFilePath);
            var yamlBytes = ReadYamlBytes(stream);

            if (!YamlCodecSupport.IsEmptyOrWhiteSpace(yamlBytes.Span))
            {
                existingDocument =
                    YamlCodecSupport.ParseYaml(yamlBytes) as YamlCodecSupport.YamlMapping
                    ?? throw new FormatException("YAML document must be a mapping.");
                options.Logger?.LogTrace("Loaded existing YAML file for partial update");
            }
        }

        // Serialize config to YAML then deserialize to dictionary
        // This goes through YAML to avoid type boxing issues (e.g. decimal)
        var configYamlBytes = YamlSerializer.Serialize(config, _serializerOptions);
        var configValue = YamlCodecSupport.ParseYaml(configYamlBytes);
        if (configValue is not YamlCodecSupport.YamlMapping configMapping)
            throw new FormatException("YAML configuration must be a mapping.");
        YamlCodecSupport.AddSchemaMetadata(
            configMapping,
            options.SchemaMetadata,
            _schemaVersionProperty
        );

        YamlCodecSupport.YamlMapping resultDocument;

        if (existingDocument == null)
        {
            // No existing file, create new nested structure
            options.Logger?.LogTrace(
                "Creating new nested section structure for section: {Section}",
                string.Join(":", sections)
            );

            resultDocument = YamlCodecSupport.CreateNestedSection(sections, configMapping);
        }
        else
        {
            // Merge with existing document
            options.Logger?.LogTrace(
                "Merging with existing YAML file for section: {Section}",
                string.Join(":", sections)
            );

            resultDocument = existingDocument;
            YamlCodecSupport.MergeSection(resultDocument, sections, 0, configMapping);
        }

        options.Logger?.LogTrace("Partial YAML serialization completed successfully");

        return SerializeForFile((YamlCodecSupport.IYamlValue)resultDocument);
    }

    private ReadOnlyMemory<byte> SerializeForFile(T value, string? schemaReference = null)
    {
        var utf8Bytes = YamlSerializer.Serialize(value, _serializerOptions);
        return AddSchemaReference(utf8Bytes, schemaReference);
    }

    private ReadOnlyMemory<byte> SerializeForFile(
        YamlCodecSupport.IYamlValue value,
        string? schemaReference = null
    )
    {
        return AddSchemaReference(YamlCodecSupport.SerializeYaml(value), schemaReference);
    }

    private ReadOnlyMemory<byte> AddSchemaReference(
        ReadOnlyMemory<byte> utf8Bytes,
        string? schemaReference
    )
    {
        string yaml;
#if NETSTANDARD2_0
        yaml = Encoding.UTF8.GetString(utf8Bytes.ToArray());
#else
        yaml = Encoding.UTF8.GetString(utf8Bytes.Span);
#endif
        if (schemaReference is not null)
            yaml =
                "# yaml-language-server: $schema=" + schemaReference + Environment.NewLine + yaml;
        return _encoding.GetBytes(yaml);
    }

    private static FileStateResource<T> GetFileResource(IStateResource resource) =>
        resource as FileStateResource<T>
        ?? throw new ArgumentException(
            "The YAML state codec requires a file state resource.",
            nameof(resource)
        );
}
