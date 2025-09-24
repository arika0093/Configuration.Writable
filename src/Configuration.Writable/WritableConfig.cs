using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public static IWritableOptions<T> GetInstance<T>()
        where T : class => GetInternalInstance<T>().GetInstance<T>();

    /// <summary>
    /// Retrieves writable configuration options for the specified options type.
    /// </summary>
    public static WritableConfigurationOptions<T> GetConfigurationOptions<T>()
        where T : class => GetInstance<T>().GetConfigurationOptions();

    /// <summary>
    /// Gets the file path of the configuration file associated with the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which to retrieve the configuration file path. Must be a reference type.</typeparam>
    /// <returns>The full file path to the configuration file for the specified type.</returns>
    public static string GetConfigFilePath<T>()
        where T : class => GetConfigurationOptions<T>().ConfigFilePath;

    /// <summary>
    /// Gets the current configuration value.
    /// </summary>
    public static T GetCurrentValue<T>()
        where T : class => GetInstance<T>().CurrentValue;

    /// <summary>
    /// Saves the specified value synchronously.
    /// </summary>
    /// <param name="value">The configuration value to save.</param>
    public static void Save<T>(T value)
        where T : class => SaveAsync<T>(value).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Updates the configuration using the specified action and saves it synchronously.
    /// </summary>
    /// <param name="action">An action to update the configuration value.</param>
    public static void Save<T>(Action<T> action)
        where T : class => SaveAsync<T>(action).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Saves the specified value asynchronously.
    /// </summary>
    /// <param name="value">The configuration value to save.</param>
    public static Task SaveAsync<T>(T value)
        where T : class => GetInstance<T>().SaveAsync(value);

    /// <summary>
    /// Updates the configuration using the specified action and saves it asynchronously.
    /// </summary>
    /// <param name="action">An action to update the configuration value.</param>
    public static Task SaveAsync<T>(Action<T> action)
        where T : class => GetInstance<T>().SaveAsync(action);
}
