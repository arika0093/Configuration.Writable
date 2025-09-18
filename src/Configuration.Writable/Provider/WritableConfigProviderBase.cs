using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration provider base class.
/// </summary>
public abstract class WritableConfigProviderBase : IWritableConfigProvider
{
    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public virtual IFileWriter FileWriter { get; set; } = new CommonFileWriter();

    /// <inheritdoc />
    public abstract void AddConfigurationFile(IConfigurationBuilder configuration, string path);

    /// <inheritdoc />
    public abstract void AddConfigurationFile(IConfigurationBuilder configuration, Stream stream);

    /// <inheritdoc />
    public abstract ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class;

    /// <inheritdoc />
    public virtual Task SaveAsync<T>(
        T config,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var contents = GetSaveContents(config, options);
        return FileWriter.SaveToFileAsync(options.ConfigFilePath, contents, cancellationToken);
    }
}
