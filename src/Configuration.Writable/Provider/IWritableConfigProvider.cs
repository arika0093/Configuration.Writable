using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Defines a provider for managing writable configurations, including serialization of configuration objects.
/// </summary>
public interface IWritableConfigProvider
{
    /// <summary>
    /// Gets the file provider used for write operations.
    /// </summary>
    public IFileWriter FileWriter { get; internal set; }

    /// <summary>
    /// Gets the file extension associated with the current file, excluding the leading period (e.g., "txt").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Adds a configuration manager to the current configuration pipeline. e.g. AddJsonFile, AddIniFile, AddXmlFile, etc.
    /// </summary>
    /// <param name="configuration">The <see cref="IConfigurationBuilder"/> instance to be added to the pipeline.</param>
    /// <param name="path">The configuration path where the manager will be applied.</param>
    void AddConfigurationFile(IConfigurationBuilder configuration, string path);

    /// <summary>
    /// Adds a configuration manager to the current configuration pipeline using the provided stream.
    /// </summary>
    /// <param name="configuration">The <see cref="IConfigurationBuilder"/> instance to be added to the pipeline.</param>
    /// <param name="stream">The stream containing the configuration data.</param>
    void AddConfigurationFile(IConfigurationBuilder configuration, Stream stream);

    /// <summary>
    /// Retrieves the serialized byte representation of the specified configuration.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object. Must be a reference type.</typeparam>
    /// <param name="config">The configuration object to be serialized.</param>
    /// <param name="options">The options that control how the configuration is serialized.</param>
    /// <returns>A read-only memory segment containing the serialized byte representation of the configuration.</returns>
    ReadOnlyMemory<byte> GetSaveContents<T>(T config, WritableConfigurationOptions<T> options)
        where T : class;

    /// <summary>
    /// Asynchronously saves the specified configuration object to a file using the provided options.
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
        where T : class;
}
