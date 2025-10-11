using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// </summary>
public interface IReadOnlyOptions<T> : IOptionsMonitor<T>
    where T : class
{
    /// <summary>
    /// Retrieves the configuration settings object for the default configuration section.
    /// </summary>
    WritableConfigurationOptions<T> GetConfigurationOptions();

    /// <summary>
    /// Retrieves the configuration settings object for the specified configuration section name.
    /// </summary>
    /// <param name="name">The name of the configuration section to retrieve options for.</param>
    WritableConfigurationOptions<T> GetConfigurationOptions(string name);
}
