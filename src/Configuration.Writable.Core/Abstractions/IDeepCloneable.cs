namespace Configuration.Writable.Abstractions;

/// <summary>
/// Interface for types that support deep cloning.
/// This interface will be implemented automatically by source generators.
/// </summary>
public interface IDeepCloneable<out T>
{
    /// <summary>
    /// Creates a deep clone of the current object.
    /// </summary>
    T DeepClone();
}
