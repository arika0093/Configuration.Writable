namespace Configuration.Writable.State;

/// <summary>
/// Describes the result of reading a state snapshot.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
public readonly record struct StateReadResult<T>(StateReadStatus Status, T? Value, string? Revision)
{
    /// <summary>Creates a successful read result.</summary>
    /// <param name="value">The loaded value.</param>
    /// <param name="revision">The opaque revision associated with the value.</param>
    /// <returns>A successful result.</returns>
    public static StateReadResult<T> Success(T value, string? revision = null) =>
        new(StateReadStatus.Success, value, revision);

    /// <summary>Creates a result for a missing state snapshot.</summary>
    /// <param name="revision">
    /// An optional opaque revision that represents the missing state for a backend that supports
    /// create-if-absent concurrency.
    /// </param>
    /// <returns>A not-found result.</returns>
    public static StateReadResult<T> NotFound(string? revision = null) =>
        new(StateReadStatus.NotFound, default, revision);

    /// <summary>Creates a result for a temporarily unavailable backend.</summary>
    /// <returns>An unavailable result.</returns>
    public static StateReadResult<T> Unavailable() =>
        new(StateReadStatus.Unavailable, default, null);
}
