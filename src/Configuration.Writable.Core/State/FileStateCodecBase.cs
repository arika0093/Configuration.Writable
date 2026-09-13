using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Migration;

namespace Configuration.Writable.State;

/// <summary>
/// Shared read/write flow for file-backed state codecs. Format-specific
/// serialization lives in the derived codecs; path resolution, backup
/// recovery, and migration wiring are centralized here.
/// </summary>
internal abstract class FileStateCodecBase<T> : IStateCodec<T>
    where T : class, new()
{
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
            type => LoadWithBackupRecovery(fileResource, readPath, type, readOptions),
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

    protected abstract object LoadCore(
        FileStateResource<T> resource,
        string path,
        Type type,
        WritableOptionsConfiguration<T> options
    );

    protected abstract OptionsSchemaMetadata? ReadSchemaMetadata(
        FileStateResource<T> resource,
        string path,
        WritableOptionsConfiguration<T> options
    );

    protected abstract ReadOnlyMemory<byte> GetSaveContents(
        T config,
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    );

    protected static FileStateResource<T> GetFileResource(IStateResource resource) =>
        resource as FileStateResource<T>
        ?? throw new ArgumentException(
            "The file state codec requires a file state resource.",
            nameof(resource)
        );

    private object LoadWithBackupRecovery(
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
}
