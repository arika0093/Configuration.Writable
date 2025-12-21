using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options monitor interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IReadOnlyOptionsMonitor<T>
    : IReadOnlyOptions<T>,
        IReadOnlyNamedOptions<T>,
        IOptionsMonitor<T>
    where T : class, new()
{
    /// <inheritdoc cref="IReadOnlyOptions{T}.CurrentValue" />
    new T CurrentValue { get; }

    /// <inheritdoc cref="IReadOnlyNamedOptions{T}.Get(string?)" />
    new T Get(string name);
}
