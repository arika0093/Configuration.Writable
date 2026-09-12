using System;

namespace Configuration.Writable.State;

internal sealed class StateSource<T>
{
    internal StateSource(
        string id,
        IStateReader<T> reader,
        IStateWriter<T>? writer,
        IStateWatcher? watcher,
        int priority,
        StateFallbackCondition fallbackCondition
    )
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("A state source id is required.", nameof(id))
            : id;
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Writer = writer;
        Watcher = watcher;
        Priority = priority;
        FallbackCondition = fallbackCondition;
    }

    internal string Id { get; }

    internal IStateReader<T> Reader { get; }

    internal IStateWriter<T>? Writer { get; }

    internal IStateWatcher? Watcher { get; }

    internal int Priority { get; }

    internal StateFallbackCondition FallbackCondition { get; }
}
