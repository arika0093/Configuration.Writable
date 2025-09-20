#pragma warning disable IDE0130
using System;
using Configuration.Writable;
using Configuration.Writable.Provider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for writable configuration.
/// </summary>
public static class WritableConfigurationExtensions
{
    /// <summary>
    /// Adds a user-defined configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to be configured. This type must be a class.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to which the configuration file and options will be added.</param>
    public static IHostApplicationBuilder AddUserConfigurationFile<T>(
        this IHostApplicationBuilder builder
    )
        where T : class
    {
        builder.Services.AddUserConfigurationFile<T>(builder.Configuration, _ => { });
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
    public static IHostApplicationBuilder AddUserConfigurationFile<T>(
        this IHostApplicationBuilder builder,
        Action<WritableConfigurationOptions<T>> configureOptions
    )
        where T : class
    {
        builder.Services.AddUserConfigurationFile<T>(builder.Configuration, configureOptions);
        return builder;
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="configuration">The existing <see cref="IConfiguration"/> instance to extend with the user-specific configuration file.</param>
    public static IServiceCollection AddUserConfigurationFile<T>(
        this IServiceCollection services,
        IConfigurationManager configuration
    )
        where T : class
    {
        return services.AddUserConfigurationFile<T>(configuration, _ => { });
    }

    /// <summary>
    /// Adds a user-specific configuration file to the application's configuration system and registers the specified
    /// options type for dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of the options to configure. This type must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the configuration and options will be added.</param>
    /// <param name="configuration">The existing <see cref="IConfiguration"/> instance to extend with the user-specific configuration file.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="WritableConfigurationOptions{T}"/> used to specify the configuration file
    /// path, section name, and other options.</param>
    public static IServiceCollection AddUserConfigurationFile<T>(
        this IServiceCollection services,
        IConfigurationManager configuration,
        Action<WritableConfigurationOptions<T>> configureOptions
    )
        where T : class
    {
        var options = new WritableConfigurationOptions<T>();
        configureOptions(options);
        var filePath = options.ConfigFilePath;
        // add configuration
        options.Provider.AddConfigurationFile(configuration, filePath);
        // add IOptions<T>
        if (string.IsNullOrWhiteSpace(options.SectionName))
        {
            services.Configure<T>(options.InstanceName, configuration);
        }
        else
        {
            services.Configure<T>(
                options.InstanceName,
                configuration.GetSection(options.SectionName)
            );
        }
        // add T instance
        if (options.RegisterInstanceToContainer)
        {
            if (string.IsNullOrEmpty(options.InstanceName))
            {
                services.AddSingleton(provider =>
                    provider.GetRequiredService<IReadonlyOptions<T>>().CurrentValue
                );
            }
            else
            {
                services.AddKeyedSingleton<T>(
                    options.InstanceName,
                    (provider, _) =>
                    {
                        return provider
                            .GetRequiredService<IReadonlyOptions<T>>()
                            .Get(options.InstanceName);
                    }
                );
            }
        }
        // add WritableConfigurationOptions<T> enumerable
        services.AddSingleton(options);
        // add IReadonlyOptions<T> and IWritableOptions<T>
        services.AddSingleton<WritableConfiguration<T>>();
        services.AddSingleton<IReadonlyOptions<T>>(p =>
            p.GetRequiredService<WritableConfiguration<T>>()
        );
        services.AddSingleton<IWritableOptions<T>>(p =>
            p.GetRequiredService<WritableConfiguration<T>>()
        );
        return services;
    }
}
