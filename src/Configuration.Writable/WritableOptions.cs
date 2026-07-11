using System;
using System.Collections.Concurrent;
using Configuration.Writable.Configure;
using Configuration.Writable.Testing;

namespace Configuration.Writable;

/// <summary>
/// Provides static utilities to initialize, retrieve, and save writable configuration.
/// </summary>
public static class WritableOptions
{
    // Store instances for different types to ensure singleton behavior per type
    private static readonly ConcurrentDictionary<Type, object> _instances = new();

    // Cache the service provider to avoid multiple builds
    private static WritableOptionsSimpleInstance<T> GetInternalInstance<T>()
        where T : class, new()
    {
        var type = typeof(T);
        return (WritableOptionsSimpleInstance<T>)
            _instances.GetOrAdd(type, _ => new WritableOptionsSimpleInstance<T>());
    }

    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public static void Initialize<T>()
        where T : class, new() => Initialize<T>(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public static void Initialize<T>(Action<WritableOptionsConfigBuilder<T>> configurationOptions)
        where T : class, new() => GetInternalInstance<T>().Initialize(configurationOptions);

    /// <summary>
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public static IWritableOptions<T> GetOptions<T>()
        where T : class, new() => GetInternalInstance<T>().GetOptions();
}
