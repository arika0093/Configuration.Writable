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
        where T : class =>
        ServiceCollection.AddUserConfigurationFile(Configuration, configurationOptions);

    /// <summary>
    /// Gets an instance of <see cref="IWritableOptions{T}"/> from the DI container.
    /// </summary>
    public static IWritableOptions<T> GetOptions<T>()
        where T : class => ServiceProvider.GetRequiredService<IWritableOptions<T>>();

    /// <summary>
    /// Gets the current configuration value.
    /// </summary>
    public static T GetValue<T>()
        where T : class => GetOptions<T>().CurrentValue;

    /// <summary>
    /// Saves the specified value synchronously.
    /// </summary>
    /// <param name="value">The configuration value to save.</param>
    public static void Save<T>(T value)
        where T : class => SaveAsync<T>(value).GetAwaiter().GetResult();

    /// <summary>
    /// Updates the configuration using the specified action and saves it synchronously.
    /// </summary>
    /// <param name="action">An action to update the configuration value.</param>
    public static void Save<T>(Action<T> action)
        where T : class => SaveAsync<T>(action).GetAwaiter().GetResult();

    /// <summary>
    /// Saves the specified value asynchronously.
    /// </summary>
    /// <param name="value">The configuration value to save.</param>
    public static Task SaveAsync<T>(T value)
        where T : class => GetOptions<T>().SaveAsync(value);

    /// <summary>
    /// Updates the configuration using the specified action and saves it asynchronously.
    /// </summary>
    /// <param name="action">An action to update the configuration value.</param>
    public static Task SaveAsync<T>(Action<T> action)
        where T : class => GetOptions<T>().SaveAsync(action);

    private static IServiceCollection ServiceCollection { get; } = new ServiceCollection();

    private static IConfigurationManager Configuration { get; } = new ConfigurationManager();

    private static IServiceProvider ServiceProvider =>
        _serviceProviderCache ??= ServiceCollection.BuildServiceProvider();
    private static IServiceProvider? _serviceProviderCache;
}
