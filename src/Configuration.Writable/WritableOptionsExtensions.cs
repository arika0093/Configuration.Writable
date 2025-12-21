using System;
using Configuration.Writable.Configure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable;

/// <summary>
/// Provides extension methods for writable configuration.
/// </summary>
public static class WritableOptionsExtensions
{
    private const string LoggerCategoryName = "Configuration.Writable";

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    public static IServiceCollection AddWritableOptions<T>(this IServiceCollection services)
        where T : class, new()
    {
        var confBuilder = new WritableOptionsConfigBuilder<T>();
        return services.AddWritableOptions<T>(MEOptions.DefaultName, confBuilder);
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="instanceName">The name of the options instance.</param>
    public static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services,
        string instanceName
    )
        where T : class, new()
    {
        var confBuilder = new WritableOptionsConfigBuilder<T>();
        return services.AddWritableOptions<T>(instanceName, confBuilder);
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="WritableOptionsConfigBuilder{T}"/> used to specify the configuration file
    /// path, section name, and other options.</param>
    public static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services,
        Action<WritableOptionsConfigBuilder<T>> configureOptions
    )
        where T : class, new()
    {
        // build options
        var confBuilder = new WritableOptionsConfigBuilder<T>();
        configureOptions(confBuilder);
        return services.AddWritableOptions<T>(MEOptions.DefaultName, confBuilder);
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="WritableOptionsConfigBuilder{T}"/> used to specify the configuration file
    /// path, section name, and other options.</param>
    public static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services,
        string instanceName,
        Action<WritableOptionsConfigBuilder<T>> configureOptions
    )
        where T : class, new()
    {
        // build options
        var confBuilder = new WritableOptionsConfigBuilder<T>();
        configureOptions(confBuilder);
        return services.AddWritableOptions<T>(instanceName, confBuilder);
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <param name="confBuilder">A pre-configured <see cref="WritableOptionsConfigBuilder{T}"/> instance used to specify the configuration file. </param>
    private static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services,
        string instanceName,
        WritableOptionsConfigBuilder<T> confBuilder
    )
        where T : class, new()
    {
        var options = confBuilder.BuildOptions(instanceName);

        // Use the actual instance name from built options (handles fallback logic)
        var actualInstanceName = options.InstanceName;

        // add T instance
        if (confBuilder.RegisterInstanceToContainer)
        {
            if (string.IsNullOrEmpty(actualInstanceName))
            {
                services.AddSingleton(provider =>
                    provider.GetRequiredService<IReadOnlyOptions<T>>().CurrentValue
                );
            }
            else
            {
                services.AddKeyedSingleton<T>(
                    actualInstanceName,
                    (provider, _) =>
                    {
                        return provider
                            .GetRequiredService<IReadOnlyNamedOptions<T>>()
                            .Get(actualInstanceName);
                    }
                );
            }
        }

        // add WritableOptionsConfiguration<T> enumerable
        if (options.Logger == null)
        {
            // Register options with a factory that resolves logger from DI
            services.AddSingleton<WritableOptionsConfiguration<T>>(provider =>
            {
                var loggerFactory = provider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger(LoggerCategoryName);
                return options with { Logger = logger };
            });
        }
        else
        {
            services.AddSingleton(options);
        }

        // add options services
        services.AddWritableOptionsCore<T>(actualInstanceName);
        return services;
    }
}
