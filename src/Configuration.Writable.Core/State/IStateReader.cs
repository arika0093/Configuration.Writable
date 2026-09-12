using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>
/// Reads a typed state snapshot from a backend.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
internal interface IStateReader<T>
{
    /// <summary>
    /// Reads the current state snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The state read result.</returns>
    ValueTask<StateReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);
}
