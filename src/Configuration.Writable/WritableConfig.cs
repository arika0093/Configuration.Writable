using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable;

/// <summary>
/// Provides static utilities to initialize, retrieve, and save writable configuration.
/// </summary>
public static class WritableConfig
{
    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public static void Initialize<T>()
        where T : class => Initialize<T>(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public static void Initialize<T>(Action<WritableConfigurationOptions<T>> configurationOptions)
        where T : class
    {
        // reset instance
        _serviceProviderCache = null;
        ServiceCollection = new ServiceCollection();
        Configuration = new ConfigurationManager();
        // add default configuration sources
        ServiceCollection.AddUserConfigurationFile(Configuration, configurationOptions);
    }

    /// <summary>
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public static IWritableOptions<T> GetInstance<T>()
        where T : class => ServiceProvider.GetRequiredService<IWritableOptions<T>>();

    /// <summary>
    /// Retrieves writable configuration options for the specified options type.
    /// </summary>
    public static WritableConfigurationOptions<T> GetConfigurationOptions<T>()
        where T : class => GetInstance<T>().GetWritableConfigurationOptions();

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

    private static IServiceCollection ServiceCollection { get; set; } = new ServiceCollection();

    private static IConfigurationManager Configuration { get; set; } = new ConfigurationManager();

    private static IServiceProvider ServiceProvider
    {
        get
        {
            if (ServiceCollection.Count == 0)
            {
                throw new InvalidOperationException(
                    "WritableConfig is not initialized. Please call Initialize<T>() first."
                );
            }
            return _serviceProviderCache ??= ServiceCollection.BuildServiceProvider();
        }
    }

    private static IServiceProvider? _serviceProviderCache;
}
