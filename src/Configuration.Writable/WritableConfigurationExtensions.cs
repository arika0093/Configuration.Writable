using System;
using Configuration.Writable.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Provides extension methods for writable configuration.
/// </summary>
public static class WritableConfigurationExtensions
{
    private const string LoggerCategoryName = "Configuration.Writable";

    /// <summary>
    /// Adds a user-defined configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to be configured. This type must be a class.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to which the configuration file and options will be added.</param>
    public static IHostApplicationBuilder AddWritableOptions<T>(
        this IHostApplicationBuilder builder
    )
        where T : class
    {
        builder.Services.AddWritableOptions<T>(_ => { });
        return builder;
    }

    /// <summary>
    /// Adds a user-defined configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to be configured. This type must be a class.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to which the configuration file and options will be added.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="WritableConfigurationOptions{T}"/> used to specify the configuration file
    /// path, section name, and other options.</param>
    public static IHostApplicationBuilder AddWritableOptions<T>(
        this IHostApplicationBuilder builder,
        Action<WritableConfigurationOptionsBuilder<T>> configureOptions
    )
        where T : class
    {
        builder.Services.AddWritableOptions<T>(configureOptions);
        return builder;
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    public static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services
    )
        where T : class
    {
        return services.AddWritableOptions<T>(_ => { });
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="WritableConfigurationOptionsBuilder{T}"/> used to specify the configuration file
    /// path, section name, and other options.</param>
    public static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services,
        Action<WritableConfigurationOptionsBuilder<T>> configureOptions
    )
        where T : class
    {
        // build options
        var confBuilder = new WritableConfigurationOptionsBuilder<T>();
        configureOptions(confBuilder);
        return services.AddWritableOptions<T>(confBuilder);
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="confBuilder">A pre-configured <see cref="WritableConfigurationOptionsBuilder{T}"/> instance used to specify the configuration file. </param>
    public static IServiceCollection AddWritableOptions<T>(
        this IServiceCollection services,
        WritableConfigurationOptionsBuilder<T> confBuilder
    )
        where T : class
    {
        var fileWriter = confBuilder.FileWriter;
        var options = confBuilder.BuildOptions();

        // set FileWriter
        if (fileWriter != null)
        {
            options.Provider.FileWriter = fileWriter;
        }

        // add T instance
        if (confBuilder.RegisterInstanceToContainer)
        {
            if (string.IsNullOrEmpty(options.InstanceName))
            {
                services.AddSingleton(provider =>
                    provider.GetRequiredService<IReadOnlyOptions<T>>().CurrentValue
                );
            }
            else
            {
                services.AddKeyedSingleton<T>(
                    options.InstanceName,
                    (provider, _) =>
                    {
                        return provider
                            .GetRequiredService<IReadOnlyOptions<T>>()
                            .Get(options.InstanceName);
                    }
                );
            }
        }

        // add WritableConfigurationOptions<T> enumerable
        if (options.Logger == null)
        {
            // Register options with a factory that resolves logger from DI
            services.AddSingleton<WritableConfigurationOptions<T>>(provider =>
            {
                var loggerFactory = provider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger(LoggerCategoryName);
                return new WritableConfigurationOptions<T>
                {
                    Provider = options.Provider,
                    ConfigFilePath = options.ConfigFilePath,
                    InstanceName = options.InstanceName,
                    SectionName = options.SectionName,
                    Logger = logger,
                    Validator = options.Validator,
                };
            });
        }
        else
        {
            services.AddSingleton(options);
        }

        // add WritableOptionsMonitor<T> (custom implementation)
        services.AddSingleton<WritableOptionsMonitor<T>>();

        // Register IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T>
        // Note: IOptions.Value should return the current value from monitor
        services.AddSingleton<IOptions<T>>(p =>
        {
            var monitor = p.GetRequiredService<WritableOptionsMonitor<T>>();
            return new DynamicOptionsWrapper<T>(monitor);
        });
        services.AddScoped<IOptionsSnapshot<T>>(p =>
        {
            var monitor = p.GetRequiredService<WritableOptionsMonitor<T>>();
            return new OptionsSnapshot<T>(monitor);
        });
        services.AddSingleton<IOptionsMonitor<T>>(p =>
            p.GetRequiredService<WritableOptionsMonitor<T>>()
        );

        // add IReadOnlyOptions<T> and IWritableOptions<T>
        services.AddSingleton<WritableConfiguration<T>>();
        services.AddSingleton<IReadOnlyOptions<T>>(p =>
            p.GetRequiredService<WritableConfiguration<T>>()
        );
        services.AddSingleton<IWritableOptions<T>>(p =>
            p.GetRequiredService<WritableConfiguration<T>>()
        );

        return services;
    }
}
