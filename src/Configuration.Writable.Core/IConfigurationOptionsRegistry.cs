using System;
using System.Collections.Generic;

namespace Configuration.Writable;

/// <summary>
/// Defines dynamic registry operations for writable configuration options of type <typeparamref name="T"/>.
/// </summary>
public interface IConfigurationOptionsRegistry<T>
    where T : class, new()
{
    /// <summary>
    /// Attempts to add a new writable configuration options of type <typeparamref name="T"/> with the specified configuration action.
    /// </summary>
    /// <param name="configure">The action to configure the writable configuration options.</param>
    bool TryAdd(Action<WritableConfigurationOptionsBuilder<T>> configure);

    /// <summary>
    /// Attempts to remove the writable configuration options with the specified instance name.
    /// </summary>
    /// <param name="instanceName"></param>
    /// <returns></returns>
    bool TryRemove(string instanceName);

    /// <summary>
    /// Clears all registered writable configuration options.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the writable configuration options with the specified instance name.
    /// </summary>
    /// <param name="instanceName">Gets the instance name of the writable configuration options.</param>
    WritableConfigurationOptions<T> Get(string instanceName);

    /// <summary>
    /// Gets all registered instance names.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetInstanceNames();

    /// <summary>
    /// Occurs when a new writable configuration option is added.
    /// </summary>
    event Action<WritableConfigurationOptions<T>> OnAdded;

    /// <summary>
    /// Occurs when an item is removed, providing the identifier of the removed item.
    /// </summary>
    event Action<string> OnRemoved;
}
