using System;
using System.Linq.Expressions;

namespace Configuration.Writable;

/// <summary>
/// Provides operations to manipulate configuration properties at the key level.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IOptionOperator<T>
    where T : class, new()
{
    /// <summary>
    /// Marks the specified property key for deletion in the configuration file.
    /// </summary>
    /// <param name="selector">An expression that selects the property to delete.</param>
    /// <remarks>
    /// This operation marks the key for deletion, which will be processed when the configuration is saved.
    /// The deletion is performed at the key level in the configuration file, not on the object instance.
    /// </remarks>
    void DeleteKey(Expression<Func<T, object?>> selector);
}
