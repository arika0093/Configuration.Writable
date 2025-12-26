using System;
using System.Collections.Generic;
using System.IO;
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
    public abstract object LoadConfiguration(Type type, Stream stream, List<string> sectionNameParts);

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
        return LoadConfiguration(typeof(T), options as WritableOptionsConfiguration<object>) as T;
    }

    /// <inheritdoc />
    public object LoadConfiguration(Type type, WritableOptionsConfiguration<object> options)
    {
        // Check if file exists
        var filePath = options.ConfigFilePath;
        if (!options.FileProvider.FileExists(filePath))
        {
            return Activator.CreateInstance(type);
        }

        var stream = options.FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return Activator.CreateInstance(type);
        }

        using (stream)
        {
            return LoadConfiguration(type, stream, options.SectionNameParts);
        }
    }
}
