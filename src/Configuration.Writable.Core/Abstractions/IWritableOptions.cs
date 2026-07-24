using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

/// <summary>
/// Interface for writable options that allows reading and updating configuration values.
/// This interface supports only unnamed access.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IWritableOptions<T> : IReadOnlyOptions<T>
    where T : class, new()
{
    /// <summary>
    /// Begins an in-memory configuration editing session.
    /// </summary>
    /// <returns>A session initialized with the current configuration value.</returns>
    ConfigureSession<T> BeginConfigure();

    /// <summary>
    /// Asynchronously saves the specified configuration.
    /// </summary>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(T newConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates and saves the configuration using the provided updater action.
    /// </summary>
    /// <param name="configUpdater">An action to update the configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates and saves the configuration using the provided asynchronous updater action.
    /// </summary>
    /// <param name="configUpdater">An asynchronous action to update the configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(Func<T, Task> configUpdater, CancellationToken cancellationToken = default);
}
