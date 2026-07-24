using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

/// <summary>
/// Provides read-only access to a set of named configuration profiles.
/// </summary>
/// <typeparam name="T">The type of the profile configuration.</typeparam>
public interface IProfiledReadOnlyOptions<T>
    where T : class, new()
{
    /// <summary>
    /// Gets the name of the active profile.
    /// </summary>
    string ActiveProfileName { get; }

    /// <summary>
    /// Gets the names of all available profiles.
    /// </summary>
    IReadOnlyCollection<string> ProfileNames { get; }

    /// <summary>
    /// Gets the current value of the active profile.
    /// </summary>
    T CurrentValue { get; }

    /// <summary>
    /// Gets the configured default profile name.
    /// </summary>
    string DefaultProfile { get; }

    /// <summary>
    /// Gets the options instance for a profile.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The options instance for the profile.</returns>
    IReadOnlyOptions<T> GetProfile(string name);

    /// <summary>
    /// Registers a listener to be called when the active profile or its value changes.
    /// </summary>
    /// <param name="listener">The action to invoke with the active profile value.</param>
    /// <returns>A disposable that unregisters the listener.</returns>
    IDisposable? OnChange(Action<T> listener);
}

/// <summary>
/// Provides read and write access to a set of named configuration profiles.
/// </summary>
/// <typeparam name="T">The type of the profile configuration.</typeparam>
public interface IProfiledWritableOptions<T> : IProfiledReadOnlyOptions<T>
    where T : class, new()
{
    /// <summary>
    /// Gets the writable options instance for a profile.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The writable options instance for the profile.</returns>
    new IWritableOptions<T> GetProfile(string name);

    /// <summary>
    /// Saves a configuration value to the active profile.
    /// </summary>
    /// <param name="newConfig">The configuration value to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(T newConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates and saves the active profile.
    /// </summary>
    /// <param name="configUpdater">An action that updates the configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates and saves the active profile.
    /// </summary>
    /// <param name="configUpdater">An asynchronous action that updates the configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(Func<T, Task> configUpdater, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a profile, optionally copying the values from an existing profile.
    /// </summary>
    /// <param name="name">The name of the new profile.</param>
    /// <param name="copyFrom">The optional source profile name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task CreateProfileAsync(
        string name,
        string? copyFrom = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes a profile from the available profile catalog.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task RemoveProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the active profile.
    /// </summary>
    /// <param name="name">The profile name to activate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default);
}
