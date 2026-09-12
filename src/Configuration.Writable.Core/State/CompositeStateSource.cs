using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>
/// Resolves prioritized state sources into one backend-neutral endpoint.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
internal sealed class CompositeStateSource<T> : IStateSource<T>
{
    private readonly StateSource<T>[] _sources;

    internal CompositeStateSource(IEnumerable<StateSource<T>> sources)
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
                return StateReadResult<T>.Success(
                    result.Value!,
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
        var source = _sources.FirstOrDefault(candidate => candidate.Writer is not null);
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
        var waits = watchers
            .Select(source =>
                source
                    .Watcher!.WaitForChangeAsync(
                        GetExpectedRevision(observedRevision, source.Id),
                        linkedCancellation.Token
                    )
                    .AsTask()
            )
            .ToArray();
        try
        {
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

    private static bool CanFallback(StateFallbackCondition condition, StateReadStatus status) =>
        status switch
        {
            StateReadStatus.NotFound => (condition & StateFallbackCondition.NotFound) != 0,
            StateReadStatus.Unavailable => (condition & StateFallbackCondition.Unavailable) != 0,
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
        var payload = new CompositeRevision(activeSourceId, copy);
        return "composite:"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private static string? GetExpectedRevision(string? compositeRevision, string sourceId)
    {
        var revision = ParseRevision(compositeRevision);
        if (revision?.Revisions.TryGetValue(sourceId, out var sourceRevision) == true)
        {
            return sourceRevision;
        }
        return null;
    }

    private static CompositeRevision? ParseRevision(string? revision)
    {
        if (revision is null || !revision.StartsWith("composite:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CompositeRevision>(
                Encoding.UTF8.GetString(Convert.FromBase64String(revision["composite:".Length..]))
            );
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CompositeRevision(
        string? ActiveSourceId,
        Dictionary<string, string?> Revisions
    );
}
