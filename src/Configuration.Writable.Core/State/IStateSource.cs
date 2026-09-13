namespace Configuration.Writable.State;

/// <summary>
/// Represents a complete state endpoint. Implementations may read, write, and observe a state
/// value without exposing the resource or codec used underneath.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
internal interface IStateSource<T> : IStateReader<T>, IStateWriter<T>, IStateWatcher { }
