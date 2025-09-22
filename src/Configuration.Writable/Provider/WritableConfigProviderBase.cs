using System;
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
    public virtual IFileWriter WriteFileProvider { get; set; } = new CommonFileWriter();

    /// <inheritdoc />
    public abstract void AddConfigurationFile(IConfigurationBuilder configuration, string path);

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
        return WriteFileProvider.SaveToFileAsync(
            options.ConfigFilePath,
            contents,
            cancellationToken
        );
    }
}
