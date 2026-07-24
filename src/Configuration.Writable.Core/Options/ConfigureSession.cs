using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

/// <summary>
/// Represents an in-memory configuration editing session.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public sealed class ConfigureSession<T>
    where T : class, new()
{
    private readonly T _loadedValue;
    private readonly T _defaultValue;
    private readonly Func<T, T> _clone;
    private readonly Func<T, CancellationToken, Task> _commit;

    internal ConfigureSession(
        T loadedValue,
        T defaultValue,
        Func<T, T> clone,
        Func<T, CancellationToken, Task> commit
    )
    {
        _clone = clone;
        _commit = commit;
        _loadedValue = _clone(loadedValue);
        _defaultValue = _clone(defaultValue);
        CurrentValue = _clone(loadedValue);
    }

    /// <summary>
    /// Gets the editable configuration value for this session.
    /// </summary>
    public T CurrentValue { get; private set; }

    /// <summary>
    /// Updates the editable configuration value.
    /// </summary>
    /// <param name="updater">An action that updates the editable value.</param>
    public void Update(Action<T> updater)
    {
        if (updater == null)
        {
            throw new ArgumentNullException(nameof(updater));
        }
        updater(CurrentValue);
    }

    /// <summary>
    /// Resets the entire editable value to the value loaded when this session began.
    /// </summary>
    public void ResetToLoaded()
    {
        CurrentValue = _clone(_loadedValue);
    }

    /// <summary>
    /// Resets selected parts of the editable value to the value loaded when this session began.
    /// </summary>
    /// <param name="reset">An action that copies selected values from the loaded value into the editable value.</param>
    public void ResetToLoaded(Action<T, T> reset)
    {
        if (reset == null)
        {
            throw new ArgumentNullException(nameof(reset));
        }
        reset(CurrentValue, _clone(_loadedValue));
    }

    /// <summary>
    /// Resets the entire editable value to a newly constructed default value.
    /// </summary>
    public void ResetToDefault()
    {
        CurrentValue = _clone(_defaultValue);
    }

    /// <summary>
    /// Resets selected parts of the editable value to a newly constructed default value.
    /// </summary>
    /// <param name="reset">An action that copies selected values from the default value into the editable value.</param>
    public void ResetToDefault(Action<T, T> reset)
    {
        if (reset == null)
        {
            throw new ArgumentNullException(nameof(reset));
        }
        reset(CurrentValue, _clone(_defaultValue));
    }

    /// <summary>
    /// Validates and saves the editable value.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        _commit(_clone(CurrentValue), cancellationToken);
}
