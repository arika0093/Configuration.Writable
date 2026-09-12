using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.Diagnostics;
using Configuration.Writable.Migration;

namespace Configuration.Writable.State;

/// <summary>
/// Temporary adapter that exposes the existing file/format pipeline through the State contracts.
/// It is deliberately isolated so that codecs and resources can replace it without changing the
/// Options runtime.
/// </summary>
/// <typeparam name="T">The options type.</typeparam>
internal sealed class LegacyFileStateSource<T> : IStateSource<T>
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;
    private readonly LegacyFileStateWatcher<T> _watcher;
    private readonly bool _acquireSaveLock;

    internal LegacyFileStateSource(
        WritableOptionsConfiguration<T> options,
        bool acquireSaveLock = true
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _watcher = new LegacyFileStateWatcher<T>(_options);
        _acquireSaveLock = acquireSaveLock;
    }

    public ValueTask<StateReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var readOptions = _options with { ConfigFilePath = _options.ReadFilePath };
        var value = readOptions.FormatProvider.LoadWithMigration<T>(readOptions);
        PromoteIfRequired(value, cancellationToken);
        return new ValueTask<StateReadResult<T>>(
            StateReadResult<T>.Success(value, GetCurrentRevision())
        );
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
            .AcquireAsync(_options.ConfigFilePath, cancellationToken)
            .ConfigureAwait(false);
        return await WriteCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<StateWriteResult> WriteCoreAsync(
        StateWriteRequest<T> request,
        CancellationToken cancellationToken
    )
    {
        if (request.ExpectedRevision is not null)
        {
            var currentRevision = GetCurrentRevision();
            if (
                currentRevision is not null
                && !string.Equals(
                    request.ExpectedRevision,
                    currentRevision,
                    StringComparison.Ordinal
                )
            )
            {
                ConfigurationWritableEventSource.Log.ConflictDetected();
                throw new ConfigurationConflictException(_options.ConfigFilePath);
            }
        }

        await _options
            .FormatProvider.SaveAsync(request.Value, _options, cancellationToken)
            .ConfigureAwait(false);
        return new StateWriteResult(GetCurrentRevision());
    }

    public ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    ) => _watcher.WaitForChangeAsync(observedRevision, cancellationToken);

    private void PromoteIfRequired(T value, CancellationToken cancellationToken)
    {
        if (
            !_options.PromoteSaveLocationEnabled
            || string.Equals(
                _options.ReadFilePath,
                _options.ConfigFilePath,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        _options
            .FormatProvider.SaveAsync(value, _options, cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    private string? GetCurrentRevision() =>
        ConfigurationFileFingerprint.Capture(_options)?.ToRevision();
}
