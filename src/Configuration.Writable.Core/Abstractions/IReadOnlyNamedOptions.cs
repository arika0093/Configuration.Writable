using System;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// This interface supports only named access.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IReadOnlyNamedOptions<T> : IReadOnlyOptionsCore<T> where T : class, new()
{
    /// <summary>
    /// Returns a configured <typeparamref name="T"/> instance with the given <paramref name="name"/>.
    /// This methods behaves similarly to the <see cref="IOptionsMonitor{T}.Get(string)"/> method.
    /// </summary>
    T Get(string name);

    /// <summary>
    /// Retrieves the configuration settings object for the specified configuration section name.
    /// </summary>
    /// <param name="name">The name of the configuration section to retrieve options for.</param>
    WritableConfigurationOptions<T> GetConfigurationOptions(string name);
}
