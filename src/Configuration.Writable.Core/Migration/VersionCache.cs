using System;
using System.Collections.Concurrent;

namespace Configuration.Writable.Migration;

/// <summary>
/// Provides a cache for version numbers of types that implement <see cref="IHasVersion"/>.
/// This avoids repeated instantiation of configuration types just to read their version.
/// </summary>
internal static class VersionCache
{
    private static readonly ConcurrentDictionary<Type, int> _versionCache = new();

    /// <summary>
    /// Gets the version number for the specified type.
    /// The version is cached after the first access.
    /// </summary>
    /// <typeparam name="T">The type that implements <see cref="IHasVersion"/>.</typeparam>
    /// <returns>The version number of the type.</returns>
    public static int GetVersion<T>()
        where T : IHasVersion, new()
    {
        return _versionCache.GetOrAdd(typeof(T), _ => new T().Version);
    }

    /// <summary>
    /// Gets the version number for the specified type.
    /// The version is cached after the first access.
    /// </summary>
    /// <param name="type">The type that implements <see cref="IHasVersion"/>.</param>
    /// <returns>The version number of the type, or null if the type doesn't implement <see cref="IHasVersion"/>.</returns>
    public static int? GetVersion(Type type)
    {
        if (!typeof(IHasVersion).IsAssignableFrom(type))
        {
            return null;
        }

        return _versionCache.GetOrAdd(
            type,
            t =>
            {
                var instance = (IHasVersion)Activator.CreateInstance(t)!;
                return instance.Version;
            }
        );
    }
}
