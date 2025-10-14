using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

/// <summary>
/// Interface for writable options that allows reading and updating configuration values.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IWritableOptions<T> : IReadOnlyOptions<T>
    where T : class
{
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
    /// Asynchronously updates and saves the configuration using the provided updater action with an operator for key-level manipulations.
    /// </summary>
    /// <param name="configUpdaterWithOperator">An action to update the configuration and perform key-level operations like deletion.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(
        Action<T, IOptionOperator<T>> configUpdaterWithOperator,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously saves the specified configuration.
    /// </summary>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="name">The name of the options instance to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveWithNameAsync(string name, T newConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates and saves the configuration using the provided updater action.
    /// </summary>
    /// <param name="configUpdater">An action to update the configuration.</param>
    /// <param name="name">The name of the options instance to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveWithNameAsync(
        string name,
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously updates and saves the configuration using the provided updater action with an operator for key-level manipulations.
    /// </summary>
    /// <param name="name">The name of the options instance to save.</param>
    /// <param name="configUpdaterWithOperator">An action to update the configuration and perform key-level operations like deletion.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveWithNameAsync(
        string name,
        Action<T, IOptionOperator<T>> configUpdaterWithOperator,
        CancellationToken cancellationToken = default
    );
}
