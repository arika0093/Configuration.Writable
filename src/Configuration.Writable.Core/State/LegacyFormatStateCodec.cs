using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Migration;

namespace Configuration.Writable.State;

/// <summary>
/// Compatibility codec for the former format-provider pipeline. Keeping this adapter at the
/// resource/codec boundary lets JSON, YAML, and XML move independently to native codecs.
/// </summary>
internal sealed class LegacyFormatStateCodec<T> : IStateCodec<T>
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
        var readOptions = options with { ConfigFilePath = options.ReadFilePath };
        return new ValueTask<T>(readOptions.FormatProvider.LoadWithMigration<T>(readOptions));
    }

    public ValueTask WriteAsync(
        T value,
        IStateResource resource,
        CancellationToken cancellationToken = default
    )
    {
        var options = GetFileResource(resource).Options;
        return new ValueTask(options.FormatProvider.SaveAsync(value, options, cancellationToken));
    }

    private static FileStateResource<T> GetFileResource(IStateResource resource) =>
        resource as FileStateResource<T>
        ?? throw new ArgumentException(
            "The legacy format codec requires a file state resource.",
            nameof(resource)
        );
}
