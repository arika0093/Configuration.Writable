using System;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a read-only configuration options interface for accessing and monitoring options of type <typeparamref name="T"/>.
/// This interface supports only unnamed access.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IReadOnlyOptions<T> : IReadOnlyOptionsCore<T>
    where T : class, new()
{
    /// <summary>
    /// Returns the current <typeparamref name="T"/> instance.
    /// This method behaves similarly to the <see cref="IOptionsMonitor{T}.CurrentValue"/> method.
    /// </summary>
    T CurrentValue { get; }

    /// <summary>
    /// Retrieves the configuration settings object for the default configuration section.
    /// </summary>
    WritableConfigurationOptions<T> GetConfigurationOptions();
}
