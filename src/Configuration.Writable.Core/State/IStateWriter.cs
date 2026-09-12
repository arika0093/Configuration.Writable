using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>
/// Writes a typed state snapshot to a backend.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
internal interface IStateWriter<T>
{
    /// <summary>
    /// Writes a state snapshot, optionally guarded by an opaque backend revision.
    /// </summary>
    /// <param name="request">The value and concurrency precondition to write.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The revision assigned to the written snapshot.</returns>
    ValueTask<StateWriteResult> WriteAsync(
        StateWriteRequest<T> request,
        CancellationToken cancellationToken = default
    );
}
