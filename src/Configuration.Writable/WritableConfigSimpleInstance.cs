using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.Writable;

/// <summary>
/// Provides methods to initialize and retrieve writable configuration instances for a specified options type.
/// </summary>
public class WritableConfigSimpleInstance<T>
    where T : class
{
    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public void Initialize() => Initialize(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Initialize(Action<WritableConfigurationOptionsBuilder<T>> configurationOptions)
    {
        Reset();
        Add(configurationOptions);
    }

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationBuilder">A pre-configured options builder to customize the configuration options.</param>
    public void Initialize(WritableConfigurationOptionsBuilder<T> configurationBuilder)
    {
        Reset();
        Add(configurationBuilder);
    }

    /// <summary>
    /// Adds writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Add(Action<WritableConfigurationOptionsBuilder<T>> configurationOptions)
    {
        // add default configuration sources
        ServiceCollection.AddWritableOptions(Configuration, configurationOptions);
    }

    /// <summary>
    /// Adds writable configuration with custom options.
    /// </summary>
    /// <param name="configurationBuilder">A pre-configured options builder to customize the configuration options.</param>
    public void Add(WritableConfigurationOptionsBuilder<T> configurationBuilder)
    {
        // add default configuration sources
        ServiceCollection.AddWritableOptions(Configuration, configurationBuilder);
    }

    /// <summary>
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public IWritableOptions<T> GetOptions() =>
        ServiceProvider.GetRequiredService<IWritableOptions<T>>();

    /// <summary>
    /// Retrieves writable configuration options for the specified options type.
    /// </summary>
    private void Reset()
    {
        _serviceProviderCache = null;
        ServiceCollection = new ServiceCollection();
        Configuration = new ConfigurationManager();
    }

    private ServiceCollection ServiceCollection { get; set; } = new();

    private IConfigurationManager Configuration { get; set; } = new ConfigurationManager();

    private IServiceProvider ServiceProvider
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

    private IServiceProvider? _serviceProviderCache;
}
