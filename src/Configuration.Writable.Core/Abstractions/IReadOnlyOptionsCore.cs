using System;
using System.ComponentModel;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface. This interface is primarily intended for internal use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IReadOnlyOptionsCore<T>
    where T : class, new()
{
    /// <summary>
    /// Registers a listener to be called whenever a named <typeparamref name="T"/> changes. <br/>
    /// This method behaves similarly to the <see cref="IOptionsMonitor{T}.OnChange"/> method.
    /// </summary>
    /// <param name="listener">The action to be invoked when <typeparamref name="T"/> has changed.</param>
    /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    IDisposable? OnChange(Action<T, string?> listener);
}
