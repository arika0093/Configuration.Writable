using System;
using System.Collections.Generic;
using System.IO;
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
    public abstract object LoadConfiguration(
        Type type,
        Stream stream,
        List<string> sectionNameParts
    );

    /// <inheritdoc />
    public virtual async ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
    {
        // Default implementation: read all data from PipeReader into a MemoryStream
        // Subclasses can override this for more efficient pipeline-based parsing
        var memoryStream = new MemoryStream();
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
#if NET8_0_OR_GREATER
                    await memoryStream.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
#else
                    await memoryStream
                        .WriteAsync(segment.ToArray(), 0, segment.Length, cancellationToken)
                        .ConfigureAwait(false);
#endif
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            memoryStream.Position = 0;
            return LoadConfiguration(type, memoryStream, sectionNameParts);
        }
        finally
        {
#if NET8_0_OR_GREATER
            await memoryStream.DisposeAsync().ConfigureAwait(false);
#else
            memoryStream.Dispose();
#endif
        }
    }

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

        // Try using PipeReader first for better performance
        var pipeReader = options.FileProvider.GetFilePipeReader(filePath);
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

        // Fallback to Stream if PipeReader is not available
        var stream = options.FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return Activator.CreateInstance(type)!;
        }

        using (stream)
        {
            return LoadConfiguration(type, stream, options.SectionNameParts);
        }
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
