using System;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// This interface supports only unnamed access.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IReadOnlyOptions<T> : IReadOnlyOptionsCore<T>
    where T : class, new()
{
    /// <summary>
    /// Returns the current <typeparamref name="T"/> instance.
    /// </summary>
    T CurrentValue { get; }

    /// <summary>
    /// Registers a listener to be called whenever the default <typeparamref name="T"/> changes.
    /// </summary>
    /// <param name="listener">The action to be invoked when <typeparamref name="T"/> has changed.</param>
    /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
    IDisposable? OnChange(Action<T> listener);
}
