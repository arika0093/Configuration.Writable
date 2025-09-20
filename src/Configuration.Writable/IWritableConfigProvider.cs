using System;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Defines a provider for managing writable configurations, including serialization of configuration objects.
/// </summary>
/// <typeparam name="T">The type of the configuration object. Must be a reference type.</typeparam>
public interface IWritableConfigProvider<T>
    where T : class
{
    /// <summary>
    /// Gets the file extension associated with the current file, including the leading period (e.g., ".txt").
    /// </summary>
    public string FileExtension { get; }

    /// <summary>
    /// Adds a configuration manager to the current configuration pipeline. e.g. AddJsonFile, AddIniFile, AddXmlFile, etc.
    /// </summary>
    /// <param name="configuration">The <see cref="IConfigurationBuilder"/> instance to be added to the pipeline.</param>
    /// <param name="path">The configuration path where the manager will be applied. This value cannot be null or empty.</param>
    void AddConfigurationFile(IConfigurationBuilder configuration, string path);

    /// <summary>
    /// Retrieves the serialized byte representation of the specified configuration.
    /// </summary>
    /// <param name="config">The configuration object to be serialized.</param>
    /// <param name="options">The options that control how the configuration is serialized.</param>
    /// <returns>A read-only memory segment containing the serialized byte representation of the configuration.</returns>
    ReadOnlyMemory<byte> GetSaveContents(T config, WritableConfigurationOptions<T> options);
}
