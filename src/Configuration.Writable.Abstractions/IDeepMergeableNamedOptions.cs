namespace Configuration.Writable;

/// <summary>
/// A named options service that can merge the source documents of multiple registered instances.
/// </summary>
/// <typeparam name="T">The options model type.</typeparam>
public interface IDeepMergeableNamedOptions<T> : IReadOnlyNamedOptions<T>
    where T : class, new()
{
    /// <summary>
    /// Deep-merges the named configuration documents in the order supplied and returns the resulting options value.
    /// Later instance names have higher precedence.
    /// </summary>
    /// <param name="instanceNames">The instance names, ordered from lowest to highest precedence.</param>
    T GetMergedValue(params string[] instanceNames);
}
