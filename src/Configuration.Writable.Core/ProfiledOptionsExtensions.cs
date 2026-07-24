using System;
using Configuration.Writable.Configure;
using Configuration.Writable.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.Writable;

/// <summary>
/// Provides extension methods for registering profiled writable options.
/// </summary>
public static class ProfiledOptionsExtensions
{
    /// <summary>
    /// Registers a set of writable configuration profiles.
    /// </summary>
    /// <typeparam name="T">The type of each profile configuration.</typeparam>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">The action used to configure the shared profile storage.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddProfiledWritableOptions<T>(
        this IServiceCollection services,
        Action<ProfiledOptionsConfigBuilder<T>> configure
    )
        where T : class, new()
    {
        var builder = new ProfiledOptionsConfigBuilder<T>();
        configure(builder);
        var configuration = builder.Build();

        services.AddSingleton(configuration.Catalog);
        services.AddWritableOptionsCore<ProfileCatalog>();
        services.AddWritableOptionsCore<T>();
        services.AddSingleton<IProfiledWritableOptions<T>>(provider =>
        {
            var options = new ProfiledWritableOptions<T>(
                provider.GetRequiredService<IWritableOptionsConfigRegistry<T>>(),
                provider.GetRequiredService<IWritableNamedOptions<T>>(),
                provider.GetRequiredService<IWritableOptions<ProfileCatalog>>(),
                builder,
                configuration
            );
            options.RegisterPersistedProfiles();
            return options;
        });
        services.AddSingleton<IProfiledReadOnlyOptions<T>>(provider =>
            provider.GetRequiredService<IProfiledWritableOptions<T>>()
        );

        return services;
    }
}
