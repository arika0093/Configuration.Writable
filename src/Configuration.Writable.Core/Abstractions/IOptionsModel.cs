using IDeepCloneable;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration options model interface.
/// </summary>
/// <typeparam name="T">The type of the options model.</typeparam>
public interface IOptionsModel<T> : IDeepCloneable<T>
    where T : class, new();

/// <summary>
/// Versioned writable configuration options model interface.
/// </summary>
/// <typeparam name="T">The type of the options model.</typeparam>
public interface IVersionedOptionsModel<T> : IOptionsModel<T>, IHasVersion
    where T : class, new();
