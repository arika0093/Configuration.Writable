using IDeepCloneable;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration options model interface.
/// </summary>
/// <typeparam name="T">The type of the options model.</typeparam>
public interface IOptionsModel<T> : IDeepCloneable<T>
    where T : class, new();
