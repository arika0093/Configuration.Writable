using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable;

/// <summary>
/// Provides static utilities to initialize, retrieve, and save writable configuration for the specified type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public static class WritableConfig<T>
    where T : class
{
    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public static void Initialize() => Initialize(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public static void Initialize(Action<WritableConfigurationOptions<T>> configurationOptions) =>
        ServiceCollection.AddUserConfigurationFile(Configuration, configurationOptions);

    /// <summary>
    /// Gets an instance of <see cref="IWritableOptions{T}"/> from the DI container.
    /// </summary>
    public static IWritableOptions<T> GetOptions() =>
        ServiceProvider.GetRequiredService<IWritableOptions<T>>();

    /// <summary>
    /// Gets the current configuration value.
    /// </summary>
    public static T GetValue() => GetOptions().CurrentValue;

    /// <summary>
    /// Saves the specified value synchronously.
    /// </summary>
    /// <param name="value">The configuration value to save.</param>
    public static void Save(T value) => SaveAsync(value).GetAwaiter().GetResult();

    /// <summary>
    /// Updates the configuration using the specified action and saves it synchronously.
    /// </summary>
    /// <param name="action">An action to update the configuration value.</param>
    public static void Save(Action<T> action) => SaveAsync(action).GetAwaiter().GetResult();

    /// <summary>
    /// Saves the specified value asynchronously.
    /// </summary>
    /// <param name="value">The configuration value to save.</param>
    public static Task SaveAsync(T value) => GetOptions().SaveAsync(value);

    /// <summary>
    /// Updates the configuration using the specified action and saves it asynchronously.
    /// </summary>
    /// <param name="action">An action to update the configuration value.</param>
    public static Task SaveAsync(Action<T> action) => GetOptions().SaveAsync(action);

    private static IServiceCollection ServiceCollection =>
        WritableConfigSharedInstance.ServiceCollection;

    private static IConfigurationManager Configuration =>
        WritableConfigSharedInstance.Configuration;

    private static IServiceProvider ServiceProvider => WritableConfigSharedInstance.ServiceProvider;
}

/// <summary>
/// Internal static class to hold shared singleton instances for configuration, service collection, and service provider.
/// </summary>
internal static class WritableConfigSharedInstance
{
    /// <summary>
    /// Gets or creates the global <see cref="IServiceProvider"/> instance.
    /// </summary>
    public static IServiceProvider ServiceProvider =>
        _serviceProviderCache ??= ServiceCollection.BuildServiceProvider();

    /// <summary>
    /// Gets the global <see cref="IServiceCollection"/> instance.
    /// </summary>
    public static IServiceCollection ServiceCollection { get; } = new ServiceCollection();

    /// <summary>
    /// Gets the global <see cref="IConfigurationManager"/> instance.
    /// </summary>
    public static IConfigurationManager Configuration { get; } = new ConfigurationManager();

    private static IServiceProvider? _serviceProviderCache;
}
