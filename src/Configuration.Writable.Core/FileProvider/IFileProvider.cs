﻿using System;
using System.IO;
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
    /// Determines whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to check. Can be either an absolute or relative path.</param>
    /// <returns>true if the file exists; otherwise, false.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Returns a stream for reading the contents of the specified file path. If the file does not exist, returns null.
    /// </summary>
    /// <param name="path">The path of the file to retrieve. Can be relative or absolute.</param>
    /// <returns>A stream containing the file contents, or null if the file does not exist.</returns>
    Stream? GetFileStream(string path);

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
}
