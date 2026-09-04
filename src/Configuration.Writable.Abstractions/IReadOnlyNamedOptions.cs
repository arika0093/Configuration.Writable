using System;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// This interface supports only named access.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IReadOnlyNamedOptions<T> : IReadOnlyOptionsCore<T>
    where T : class, new()
{
    /// <summary>
    /// Returns a configured <typeparamref name="T"/> instance with the given <paramref name="name"/>.
    /// </summary>
    T Get(string name);

    /// <summary>
    /// Retrieves a read-only options instance bound to the specified instanceName.
    /// This allows you to work with named instances using the same API as regular <see cref="IReadOnlyOptions{T}"/>,
    /// without having to specify the name on every operation.
    /// </summary>
    /// <param name="name">The name of the instance to bind to.</param>
    /// <returns>An <see cref="IReadOnlyOptions{T}"/> instance bound to the specified name.</returns>
    IReadOnlyOptions<T> GetInstance(string name);

    /// <summary>
    /// Registers a listener to be called whenever the matching named <typeparamref name="T"/> changes.
    /// </summary>
    /// <param name="name">The name of the options instance to listen for changes.</param>
    /// <param name="listener">The action to be invoked when <typeparamref name="T"/> has changed.</param>
    /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
    IDisposable? OnChange(string name, Action<T> listener);
}
