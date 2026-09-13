namespace Configuration.Writable.State;

/// <summary>
/// Describes the successful result of writing a state snapshot.
/// </summary>
/// <param name="Revision">The opaque revision assigned by the backend.</param>
public readonly record struct StateWriteResult(string? Revision);
