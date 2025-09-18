﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Interface for writable options that allows reading and updating configuration values.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IWritableOptions<T> : IOptions<T>, IOptionsMonitor<T>
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
    /// <param name="configUpdator">An action to update the configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SaveAsync(Action<T> configUpdator, CancellationToken cancellationToken = default);
}
