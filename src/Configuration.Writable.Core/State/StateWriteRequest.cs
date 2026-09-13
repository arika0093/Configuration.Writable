namespace Configuration.Writable.State;

/// <summary>
/// Describes a state write and its optional optimistic-concurrency precondition.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
public readonly record struct StateWriteRequest<T>(T Value, string? ExpectedRevision);
