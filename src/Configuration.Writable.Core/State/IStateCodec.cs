using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>Converts between a state value and the content stored by a resource.</summary>
/// <typeparam name="T">The state type.</typeparam>
internal interface IStateCodec<T>
{
    ValueTask<T> ReadAsync(IStateResource resource, CancellationToken cancellationToken = default);

    ValueTask WriteAsync(
        T value,
        IStateResource resource,
        CancellationToken cancellationToken = default
    );
}
