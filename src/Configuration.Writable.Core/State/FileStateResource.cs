using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>
/// File-backed state resource. It owns file identity, revision calculation, and watching;
/// serialization is intentionally delegated to a codec.
/// </summary>
internal sealed class FileStateResource<T> : IStateResource
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;
    private readonly LegacyFileStateWatcher<T> _watcher;

    internal FileStateResource(WritableOptionsConfiguration<T> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _watcher = new LegacyFileStateWatcher<T>(_options);
    }

    internal WritableOptionsConfiguration<T> Options => _options;

    public string? GetRevision() => ConfigurationFileFingerprint.Capture(_options)?.ToRevision();

    public ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    ) => _watcher.WaitForChangeAsync(observedRevision, cancellationToken);
}
