using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.Diagnostics;

namespace Configuration.Writable.State;

/// <summary>Composes a file resource and a codec into one state endpoint.</summary>
internal sealed class FileStateSource<T> : IStateSource<T>
    where T : class, new()
{
    private readonly FileStateResource<T> _resource;
    private readonly IStateCodec<T> _codec;
    private readonly bool _acquireSaveLock;

    internal FileStateSource(WritableOptionsConfiguration<T> options, bool acquireSaveLock = true)
    {
        _resource = new FileStateResource<T>(options);
        _codec = new LegacyFormatStateCodec<T>();
        _acquireSaveLock = acquireSaveLock;
    }

    public async ValueTask<StateReadResult<T>> ReadAsync(
        CancellationToken cancellationToken = default
    )
    {
        var value = await _codec.ReadAsync(_resource, cancellationToken).ConfigureAwait(false);
        await PromoteIfRequiredAsync(value, cancellationToken).ConfigureAwait(false);
        return StateReadResult<T>.Success(value, _resource.GetRevision());
    }

    public async ValueTask<StateWriteResult> WriteAsync(
        StateWriteRequest<T> request,
        CancellationToken cancellationToken = default
    )
    {
        if (!_acquireSaveLock)
        {
            return await WriteCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }

        using var fileLock = await AsyncFileSaveLock
            .AcquireAsync(_resource.Options.ConfigFilePath, cancellationToken)
            .ConfigureAwait(false);
        return await WriteCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    ) => _resource.WaitForChangeAsync(observedRevision, cancellationToken);

    private async ValueTask<StateWriteResult> WriteCoreAsync(
        StateWriteRequest<T> request,
        CancellationToken cancellationToken
    )
    {
        var currentRevision = _resource.GetRevision();
        if (
            request.ExpectedRevision is not null
            && currentRevision is not null
            && !string.Equals(request.ExpectedRevision, currentRevision, StringComparison.Ordinal)
        )
        {
            ConfigurationWritableEventSource.Log.ConflictDetected();
            throw new ConfigurationConflictException(_resource.Options.ConfigFilePath);
        }

        await _codec.WriteAsync(request.Value, _resource, cancellationToken).ConfigureAwait(false);
        return new StateWriteResult(_resource.GetRevision());
    }

    private async ValueTask PromoteIfRequiredAsync(T value, CancellationToken cancellationToken)
    {
        var options = _resource.Options;
        if (
            !options.PromoteSaveLocationEnabled
            || string.Equals(options.ReadFilePath, options.ConfigFilePath, StringComparison.Ordinal)
        )
        {
            return;
        }

        await _codec.WriteAsync(value, _resource, cancellationToken).ConfigureAwait(false);
    }
}
