using Configuration.Writable.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable;

/// <summary>
/// Provides extension methods for registering writable options services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class WritableOptionsCoreExtensions
{
    /// <summary>
    /// Registers writable options services for the specified options type.
    /// </summary>
    /// <typeparam name="T">The type of the options class to configure. This type must be a reference type.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the writable options services will be added.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance with the writable options services registered.</returns>
    public static IServiceCollection AddWritableOptionsCore<T>(this IServiceCollection services)
        where T : class, new() => services.AddWritableOptionsCore<T>(MEOptions.DefaultName);

    /// <summary>
    /// Registers writable options services for the specified options type.
    /// </summary>
    /// <typeparam name="T">The type of the options class to configure. This type must be a reference type.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the writable options services will be added.</param>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance with the writable options services registered.</returns>
    public static IServiceCollection AddWritableOptionsCore<T>(
        this IServiceCollection services,
        string instanceName
    )
        where T : class, new()
    {
        // add IWritableOptionsConfigRegistry<T>
        services.AddSingleton<
            IWritableOptionsConfigRegistry<T>,
            WritableOptionsConfigRegistryImpl<T>
        >();

        // add WritableOptionsMonitor<T> (custom implementation)
        services.AddSingleton<OptionsMonitorImpl<T>>();

        // Register IOptions<T>, IOptionsSnapshot<T> and IOptionsMonitor<T>
        services.AddSingleton<IOptions<T>, OptionsImpl<T>>();
        services.AddScoped<IOptionsSnapshot<T>, OptionsSnapshotImpl<T>>();
        services.AddSingleton<IOptionsMonitor<T>>(p =>
            p.GetRequiredService<OptionsMonitorImpl<T>>()
        );

        // add IReadOnlyOptions<T> and IWritableOptions<T>
        services.AddSingleton<WritableOptionsImpl<T>>();
        services.AddSingleton<IReadOnlyOptions<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );
        services.AddSingleton<IReadOnlyNamedOptions<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );
        services.AddSingleton<IWritableOptions<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );
        services.AddSingleton<IWritableNamedOptions<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );
        services.AddSingleton<IReadOnlyOptionsMonitor<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );
        services.AddSingleton<IWritableOptionsMonitor<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );

        // if named instance, add named wrapper
        if (!string.IsNullOrEmpty(instanceName))
        {
            services.AddKeyedSingleton<IWritableOptions<T>>(
                instanceName,
                (p, key) =>
                    new WritableOptionsWithNameImpl<T>(
                        p.GetRequiredService<WritableOptionsImpl<T>>(),
                        instanceName
                    )
            );
            services.AddKeyedSingleton<IReadOnlyOptions<T>>(
                instanceName,
                (p, key) => p.GetRequiredKeyedService<IWritableOptions<T>>(instanceName)
            );
            services.AddKeyedSingleton<IReadOnlyOptionsMonitor<T>>(
                instanceName,
                (p, key) => p.GetRequiredService<WritableOptionsImpl<T>>()
            );
            services.AddKeyedSingleton<IWritableOptionsMonitor<T>>(
                instanceName,
                (p, key) => p.GetRequiredService<WritableOptionsImpl<T>>()
            );
        }
        return services;
    }
}
