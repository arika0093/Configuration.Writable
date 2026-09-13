using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>
/// Resolves prioritized state sources into one backend-neutral endpoint.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
internal sealed class CompositeStateSource<T> : IStateSource<T>
{
    private const string CompositeRevisionPrefix = "composite:v1:";
    private readonly StateSource<T>[] _sources;
    private readonly string? _writeTargetId;

    internal CompositeStateSource(IEnumerable<StateSource<T>> sources, string? writeTargetId = null)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }
        _sources = sources
            .Select((source, index) => new { Source = source, Index = index })
            .OrderByDescending(entry => entry.Source.Priority)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Source)
            .ToArray();

        if (_sources.Length == 0)
        {
            throw new ArgumentException("At least one state source is required.", nameof(sources));
        }
        if (
            _sources.Select(source => source.Id).Distinct(StringComparer.Ordinal).Count()
            != _sources.Length
        )
        {
            throw new ArgumentException("State source ids must be unique.", nameof(sources));
        }
        if (
            writeTargetId is not null
            && !_sources.Any(source =>
                string.Equals(source.Id, writeTargetId, StringComparison.Ordinal)
            )
        )
        {
            throw new ArgumentException(
                "The write target must be a registered state source.",
                nameof(writeTargetId)
            );
        }
        _writeTargetId = writeTargetId;
    }

    public async ValueTask<StateReadResult<T>> ReadAsync(
        CancellationToken cancellationToken = default
    )
    {
        var revisions = new Dictionary<string, string?>(StringComparer.Ordinal);
        StateReadStatus lastStatus = StateReadStatus.NotFound;

        foreach (var source in _sources)
        {
            var result = await source.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            revisions[source.Id] = result.Revision;

            if (result.Status == StateReadStatus.Success)
            {
                var value =
                    result.Value
                    ?? throw new InvalidOperationException(
                        $"State source '{source.Id}' returned Success without a value."
                    );
                return StateReadResult<T>.Success(
                    value,
                    StoreRevisions(revisions, source.Id, result.Revision)
                );
            }

            lastStatus = result.Status;
            if (!CanFallback(source.FallbackCondition, result.Status))
            {
                return result.Status == StateReadStatus.NotFound
                    ? StateReadResult<T>.NotFound(
                        StoreRevisions(revisions, source.Id, result.Revision)
                    )
                    : StateReadResult<T>.Unavailable();
            }
        }

        return lastStatus == StateReadStatus.NotFound
            ? StateReadResult<T>.NotFound(StoreRevisions(revisions, null, null))
            : StateReadResult<T>.Unavailable();
    }

    public async ValueTask<StateWriteResult> WriteAsync(
        StateWriteRequest<T> request,
        CancellationToken cancellationToken = default
    )
    {
        var source = _writeTargetId is null
            ? _sources.FirstOrDefault(candidate => candidate.Writer is not null)
            : _sources.First(candidate =>
                string.Equals(candidate.Id, _writeTargetId, StringComparison.Ordinal)
            );
        if (source?.Writer is null)
        {
            throw new InvalidOperationException("No writable state source is configured.");
        }

        var expectedRevision = GetExpectedRevision(request.ExpectedRevision, source.Id);
        var result = await source
            .Writer.WriteAsync(
                new StateWriteRequest<T>(request.Value, expectedRevision),
                cancellationToken
            )
            .ConfigureAwait(false);

        return new StateWriteResult(
            StoreRevisions(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [source.Id] = result.Revision,
                },
                source.Id,
                result.Revision
            )
        );
    }

    public async ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    )
    {
        var revision = ParseRevision(observedRevision);
        var activeSource = revision?.ActiveSourceId is null
            ? null
            : _sources.FirstOrDefault(source =>
                string.Equals(source.Id, revision.ActiveSourceId, StringComparison.Ordinal)
            );
        var minimumPriority = activeSource?.Priority ?? int.MinValue;
        var watchers = _sources
            .Where(source => source.Watcher is not null && source.Priority >= minimumPriority)
            .ToArray();
        if (watchers.Length == 0)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        try
        {
            var waits = watchers
                .Select(source =>
                {
                    var watcher = source.Watcher;
                    if (watcher is null)
                    {
                        return Task.CompletedTask;
                    }
                    return watcher
                        .WaitForChangeAsync(
                            GetExpectedRevision(observedRevision, source.Id),
                            linkedCancellation.Token
                        )
                        .AsTask();
                })
                .ToArray();
            await (await Task.WhenAny(waits).ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
#if NET8_0_OR_GREATER
            await linkedCancellation.CancelAsync().ConfigureAwait(false);
#else
            linkedCancellation.Cancel();
#endif
        }
    }

    private static bool CanFallback(StateFallbackConditions condition, StateReadStatus status) =>
        status switch
        {
            StateReadStatus.NotFound => (condition & StateFallbackConditions.NotFound) != 0,
            StateReadStatus.Unavailable => (condition & StateFallbackConditions.Unavailable) != 0,
            _ => false,
        };

    private static string StoreRevisions(
        IReadOnlyDictionary<string, string?> revisions,
        string? activeSourceId,
        string? activeRevision
    )
    {
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var entry in revisions)
        {
            copy[entry.Key] = entry.Value;
        }
        if (activeSourceId is not null)
        {
            copy[activeSourceId] = activeRevision;
        }

        var payload = new StringBuilder(CompositeRevisionPrefix);
        payload.Append(EncodeRevisionPart(activeSourceId));
        foreach (var entry in copy.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            payload
                .Append(';')
                .Append(EncodeRequiredPart(entry.Key))
                .Append(':')
                .Append(EncodeRevisionPart(entry.Value));
        }
        return payload.ToString();
    }

    private static string? GetExpectedRevision(string? compositeRevision, string sourceId)
    {
        var revision = ParseRevision(compositeRevision);
        if (
            revision is not null
            && revision.Revisions.TryGetValue(sourceId, out var sourceRevision)
        )
        {
            return sourceRevision;
        }
        return null;
    }

    private static CompositeRevision? ParseRevision(string? revision)
    {
        if (
            revision is null
            || !revision.StartsWith(CompositeRevisionPrefix, StringComparison.Ordinal)
        )
        {
            return null;
        }

        try
        {
            var parts = revision[CompositeRevisionPrefix.Length..].Split(';');
            if (parts.Length == 0)
            {
                return null;
            }

            var activeSourceId = DecodeRevisionPart(parts[0]);
            var revisions = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var index = 1; index < parts.Length; index++)
            {
                var separator = parts[index].IndexOf(':');
                if (separator < 0)
                {
                    return null;
                }

                var sourceId = DecodeRequiredPart(parts[index][..separator]);
                if (sourceId is null || revisions.ContainsKey(sourceId))
                {
                    return null;
                }

                revisions[sourceId] = DecodeRevisionPart(parts[index][(separator + 1)..]);
            }

            return new CompositeRevision(activeSourceId, revisions);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string EncodeRevisionPart(string? value) =>
        value is null ? "-" : EncodeRequiredPart(value);

    private static string EncodeRequiredPart(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string? DecodeRevisionPart(string value) =>
        string.Equals(value, "-", StringComparison.Ordinal) ? null : DecodeRequiredPart(value);

    private static string? DecodeRequiredPart(string value)
    {
        if (string.Equals(value, "-", StringComparison.Ordinal))
        {
            return null;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }

    private sealed record CompositeRevision(
        string? ActiveSourceId,
        Dictionary<string, string?> Revisions
    );
}
