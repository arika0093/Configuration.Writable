using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Provides static utilities to initialize, retrieve, and save writable configuration.
/// </summary>
public static class WritableConfig
{
    // Store instances for different types to ensure singleton behavior per type
    private static readonly Dictionary<Type, WritableConfigSimpleInstance> _instances = new();

    // Cache the service provider to avoid multiple builds
    private static WritableConfigSimpleInstance GetInternalInstance<T>()
        where T : class
    {
        var type = typeof(T);
        if (!_instances.TryGetValue(type, out WritableConfigSimpleInstance? value))
        {
            value = new WritableConfigSimpleInstance();
            _instances[type] = value;
        }
        return value;
    }

    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public static void Initialize<T>()
        where T : class => Initialize<T>(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public static void Initialize<T>(
        Action<WritableConfigurationOptionsBuilder<T>> configurationOptions
    )
        where T : class => GetInternalInstance<T>().Initialize(configurationOptions);

    /// <summary>
    /// Initializes writable configuration with a logger for Console applications.
    /// </summary>
    /// <param name="logger">The logger to use for configuration operations.</param>
    public static void Initialize<T>(ILogger logger)
        where T : class => Initialize<T>(options => options.Logger = logger);

    /// <summary>
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public static IWritableOptions<T> GetOption<T>()
        where T : class => GetInternalInstance<T>().GetOption<T>();
}
