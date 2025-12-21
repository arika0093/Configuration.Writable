namespace Configuration.Writable;

/// <summary>
/// Represents a writable configuration options monitor interface for accessing, monitoring, and saving options of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IWritableOptionsMonitor<T>
    : IWritableOptions<T>,
        IWritableNamedOptions<T>,
        IReadOnlyOptionsMonitor<T>
    where T : class, new()
{
    /// <inheritdoc cref="IReadOnlyOptions{T}.CurrentValue" />
    new T CurrentValue { get; }

    /// <inheritdoc cref="IReadOnlyNamedOptions{T}.Get(string?)" />
    new T Get(string name);
}
