using System;
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

    /// <inheritdoc cref="IReadOnlyOptionsCore{T}.OnChange" />
    new IDisposable? OnChange(Action<T, string?> listener);

    /// <summary>
    /// Registers a listener to be called when a configuration reload fails.
    /// The last successfully loaded value remains available after a failure.
    /// </summary>
    /// <param name="listener">The action to invoke with the failure and options instance name.</param>
    /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for failures.</returns>
    IDisposable? OnReloadFailed(Action<Exception, string?> listener);
}
