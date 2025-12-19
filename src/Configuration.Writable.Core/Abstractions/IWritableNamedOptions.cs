using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

/// <summary>
/// Interface for writable options that allows reading and updating configuration values.
/// This interface supports only named access.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IWritableNamedOptions<T> : IReadOnlyNamedOptions<T>
    where T : class, new()
{
    /// <summary>
    /// Retrieves a writable options instance bound to the specified instanceName.
    /// This allows you to work with named instances using the same API as regular <see cref="IWritableOptions{T}"/>,
    /// without having to specify the name on every operation.
    /// </summary>
    /// <param name="name">The name of the instance to bind to.</param>
    /// <returns>An <see cref="IWritableOptions{T}"/> instance bound to the specified name.</returns>
    new IWritableOptions<T> GetSpecifiedInstance(string name);

    /// <summary>
    /// Asynchronously saves the specified configuration.
    /// </summary>
    /// <param name="name">The name of the options instance to save.</param>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(string name, T newConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates and saves the configuration using the provided updater action.
    /// </summary>
    /// <param name="name">The name of the options instance to save.</param>
    /// <param name="configUpdater">An action to update the configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(
        string name,
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    );
}
