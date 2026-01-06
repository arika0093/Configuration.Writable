using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>
/// Defines a provider for writing data to files.
/// </summary>
public interface IFileProvider
{
    /// <summary>
    /// Returns a PipeReader for reading the contents of the specified file path. If the file does not exist, returns null.
    /// </summary>
    /// <param name="path">The path of the file to retrieve. Can be relative or absolute.</param>
    /// <returns>A PipeReader containing the file contents, or null if the file does not exist.</returns>
    PipeReader? GetFilePipeReader(string path);

    /// <summary>
    /// Asynchronously saves the specified content to a file at the given path.
    /// </summary>
    /// <param name="path">The full file path where the content will be saved.</param>
    /// <param name="content">The content to write to the file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="logger">An optional logger for logging operations and errors.</param>
    Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Determines whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to check. Can be either an absolute or relative path.</param>
    /// <returns>true if the file exists; otherwise, false.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Determines whether a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path of the directory to check. Can be either an absolute or relative path.</param>
    /// <returns>true if the directory exists; otherwise, false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Checks if the specified file can be opened with write access.
    /// </summary>
    /// <param name="path">The path of the file to check for write access.</param>
    /// <returns>true if the file exists and can be opened with write access; otherwise, false.</returns>
    bool CanWriteToFile(string path);

    /// <summary>
    /// Checks if the specified directory can be written to by attempting to create a temporary file.
    /// </summary>
    /// <param name="path">The file path whose directory will be checked for write access.</param>
    /// <returns>true if the directory exists and can be written to; otherwise, false.</returns>
    bool CanWriteToDirectory(string path);
}
