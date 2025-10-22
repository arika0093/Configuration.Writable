using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;

namespace Configuration.Writable;

/// <summary>
/// Defines a provider for managing writable configurations, including serialization and deserialization of configuration objects.
/// </summary>
public interface IWritableConfigProvider
{
    /// <summary>
    /// Gets the file provider used for write operations.
    /// </summary>
    public IFileProvider FileProvider { get; set; }

    /// <summary>
    /// Gets the file extension associated with the current file, excluding the leading period (e.g., "txt").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Loads configuration from a file and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object. Must be a reference type.</typeparam>
    /// <param name="options">The options that control how the configuration is loaded.</param>
    /// <returns>The deserialized configuration object.</returns>
    T LoadConfiguration<T>(WritableConfigurationOptions<T> options)
        where T : class, new();

    /// <summary>
    /// Loads configuration from a stream and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object. Must be a reference type.</typeparam>
    /// <param name="stream">The stream containing the configuration data.</param>
    /// <param name="options">The options that control how the configuration is loaded.</param>
    /// <returns>The deserialized configuration object.</returns>
    T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
        where T : class, new();

    /// <summary>
    /// Asynchronously saves the specified configuration object to a file.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to save. Must be a reference type.</typeparam>
    /// <param name="config">The configuration object to be saved.</param>
    /// <param name="options">The options that control how and where the configuration is saved, including the target file path.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the save operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveAsync<T>(
        T config,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class, new();
}
