using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>
/// Waits until a backend state snapshot may have changed.
/// </summary>
public interface IStateWatcher
{
    /// <summary>
    /// Waits for an invalidation of the state observed at <paramref name="observedRevision"/>.
    /// A successful return is only an invalidation signal; callers must read again to obtain the
    /// authoritative value and revision.
    /// </summary>
    /// <param name="observedRevision">The opaque revision that was last observed.</param>
    /// <param name="cancellationToken">A token used to cancel the wait.</param>
    ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    );
}
