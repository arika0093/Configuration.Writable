using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Writable configuration provider base class.
/// </summary>
public abstract class FormatProviderBase : IFormatProvider
{
    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public abstract ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc />
    public abstract Task SaveAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
        CancellationToken cancellationToken = default
    )
        where T : class, new();

    /// <summary>
    /// Attempts to read the version declared in the configuration file without fully deserializing it.
    /// </summary>
    /// <param name="options">The options that control how the configuration is loaded.</param>
    /// <returns>The version declared in the file, or <see langword="null"/> if the file does not exist or does not declare a version.</returns>
    internal virtual int? TryGetFileVersion(IWritableOptionsConfiguration options) => null;

    /// <inheritdoc />
    public object LoadConfiguration(Type type, IWritableOptionsConfiguration options)
    {
        // Check if file exists
        var filePath = options.ConfigFilePath;
        if (!options.FileProvider.FileExists(filePath))
        {
            return Activator.CreateInstance(type)!;
        }

        // Use PipeReader for reading
        var pipeReader = options.FileProvider.GetFilePipeReader(filePath);
        if (pipeReader == null)
        {
            return Activator.CreateInstance(type)!;
        }

        // PipeReader.Create returns a type that implements IDisposable
        // We use synchronous disposal here since we're in a sync method
        try
        {
            return LoadConfigurationAsync(
                    type,
                    pipeReader,
                    options.SectionNameParts,
                    CancellationToken.None
                )
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            if (pipeReader is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Loads configuration from a file and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object. Must be a reference type.</typeparam>
    /// <param name="options">The options that control how the configuration is loaded.</param>
    /// <returns>The deserialized configuration object.</returns>
    [Obsolete("Use LoadConfiguration(Type, IWritableOptionsConfiguration) instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual T LoadConfiguration<T>(WritableOptionsConfiguration<T> options)
        where T : class, new()
    {
        var rst = LoadConfiguration(typeof(T), options)!;
        return (T)rst;
    }

    /// <summary>
    /// Loads configuration from a file and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object. Must be a reference type.</typeparam>
    /// <param name="type">The type of the configuration object to load.</param>
    /// <param name="options">The options that control how the configuration is loaded.</param>
    /// <returns>The deserialized configuration object.</returns>
    [Obsolete("Use LoadConfiguration(Type, IWritableOptionsConfiguration) instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual object LoadConfiguration<T>(Type type, WritableOptionsConfiguration<T> options)
        where T : class, new()
        => LoadConfiguration(type, (IWritableOptionsConfiguration)options);

    /// <summary>
    /// Asynchronously saves the specified configuration object to a file.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to save. Must be a reference type.</typeparam>
    /// <param name="config">The configuration object to be saved.</param>
    /// <param name="options">The options that control how and where the configuration is saved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the save operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    [Obsolete("Use SaveAsync<T>(T, IWritableOptionsConfiguration, CancellationToken) instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual Task SaveAsync<T>(
        T config,
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
        => SaveAsync(config, (IWritableOptionsConfiguration)options, cancellationToken);

    /// <summary>
    /// Creates a nested dictionary structure from a section name that supports ':' and '__' as separators.
    /// For example, "SectionA:SectionB" or "SectionA__SectionB" will create { "SectionA": { "SectionB": value } }.
    /// </summary>
    /// <param name="parts">The list of section name parts split by the separators.</param>
    /// <param name="value">The value to place at the deepest level.</param>
    /// <returns>A nested dictionary representing the section hierarchy, or the original value if no separators are found.</returns>
    protected static object CreateNestedSection(List<string> parts, object value)
    {
        if (parts.Count <= 0)
        {
            // No separators found, return a simple dictionary
            return value;
        }

        // Build nested structure from the inside out
        object current = value;
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            current = new Dictionary<string, object> { [parts[i]] = current };
        }

        return current;
    }
}
