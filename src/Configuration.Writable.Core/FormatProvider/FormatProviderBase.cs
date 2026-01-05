using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

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
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class, new();

    /// <inheritdoc />
    public T LoadConfiguration<T>(WritableOptionsConfiguration<T> options)
        where T : class, new()
    {
        var rst = LoadConfiguration(typeof(T), options)!;
        return (T)rst;
    }

    /// <inheritdoc />
    public object LoadConfiguration<T>(Type type, WritableOptionsConfiguration<T> options)
        where T : class, new()
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
        if (pipeReader is IDisposable disposable)
        {
            using (disposable)
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
        }

        return Activator.CreateInstance(type)!;
    }

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
