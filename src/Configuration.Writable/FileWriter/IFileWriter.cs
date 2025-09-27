using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileWriter;

/// <summary>
/// Defines a provider for writing data to files.
/// </summary>
public interface IFileWriter
{
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
        CancellationToken cancellationToken = default,
        ILogger? logger = null
    );
}
