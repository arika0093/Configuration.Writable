using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// <see cref="IOptionsMonitor{T}"/> may not work properly in some environments (docker, network shares, etc.). <br/>
/// <see cref="IReadonlyOptions{T}"/> is an interface to reliably obtain settings updated by <see cref="IWritableOptions{T}"/>.
/// </remarks>
public interface IReadonlyOptions<out T> : IOptionsMonitor<T>
    where T : class;
