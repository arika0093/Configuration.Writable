using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// <see cref="IOptionsMonitor{T}"/> may not work properly in some environments (docker, network shares, etc.). <br/>
/// <see cref="IReadonlyOptions{T}"/> is an interface to reliably obtain settings updated by <see cref="IWritableOptions{T}"/>.
/// </remarks>
public interface IReadonlyOptions<T> : IOptionsMonitor<T>
    where T : class
{
    /// <summary>
    /// Retrieves a writable configuration settings object for the default configuration section.
    /// </summary>
    WritableConfigurationOptions<T> GetWritableConfigurationOptions();

    /// <summary>
    /// Retrieves a writable configuration settings object for the specified configuration section name.
    /// </summary>
    /// <param name="name">The name of the configuration section to retrieve options for.</param>
    WritableConfigurationOptions<T> GetWritableConfigurationOptions(string name);
}
