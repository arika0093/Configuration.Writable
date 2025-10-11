using Configuration.Writable.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        where T : class
    {
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
        services.AddSingleton<IWritableOptions<T>>(p =>
            p.GetRequiredService<WritableOptionsImpl<T>>()
        );
        return services;
    }
}
