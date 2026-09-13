using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.State;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// In-memory <see cref="IFileBackend"/> implementation for testing purposes.
/// </summary>
internal class InMemoryFileBackend : IFileBackend
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public int BackupAttemptCount { get; private set; }

    public bool IsPhysical => false;

    public string GetPhysicalPath(string path) => Path.GetFullPath(path);

    /// <inheritdoc />
    public bool TryBackup(string path, out string? backupPath, ILogger? logger = null)
    {
        BackupAttemptCount++;
        backupPath = null;
        return false;
    }

    public bool TryDelete(string path, ILogger? logger = null)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _files.TryRemove(normalizedPath, out _);
    }

    public bool TryRestoreLatestBackup(string path, ILogger? logger = null) => false;

    /// <inheritdoc />
    public Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = Path.GetFullPath(path);
        _files[normalizedPath] = content.ToArray();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to check. Can be either an absolute or relative path.</param>
    public bool FileExists(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _files.ContainsKey(normalizedPath);
    }

    /// <inheritdoc />
    public Stream? OpenReadStream(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (!_files.TryGetValue(normalizedPath, out var content))
        {
            return null;
        }
        return new MemoryStream(content, writable: false);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        // In-memory backend always has directories available
        return true;
    }

    /// <inheritdoc />
    public bool CanWriteToFile(string path)
    {
        // In-memory backend can write to a file if it exists
        return FileExists(path);
    }

    /// <inheritdoc />
    public bool CanWriteToDirectory(string path)
    {
        // In-memory backend can always write to any directory
        return true;
    }

    /// <inheritdoc />
    public bool EnsureDirectoryExists(string path)
    {
        // In-memory backend always has directories available
        return true;
    }

    /// <summary>
    /// Reads the contents of the file at the specified path and returns them as a byte array.
    /// </summary>
    /// <param name="path">The path to the file to read. The path can be relative or absolute.</param>
    public byte[] ReadAllBytes(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (!_files.TryGetValue(normalizedPath, out var content))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }
        return content;
    }

    /// <summary>
    /// Reads all text from the specified file using UTF-8 encoding.
    /// </summary>
    /// <param name="path">The relative or absolute path to the file to read. The path is not case-sensitive.</param>
    public string ReadAllText(string path)
    {
        var bytes = ReadAllBytes(path);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Retrieves the names of files in the specified directory that match the given search pattern.
    /// </summary>
    /// <param name="directory">The path to the directory to search. This must be a valid directory path.</param>
    /// <param name="pattern">The search pattern to match against file names. The default is "*", which matches all files. The pattern may
    /// include a single asterisk ('*') as a wildcard.</param>
    public string[] GetFiles(string directory, string pattern = "*")
    {
        var normalizedDirectory = Path.GetFullPath(directory);
        var result = new List<string>();

        foreach (var filePath in _files.Keys)
        {
            var fileDirectory = Path.GetDirectoryName(filePath);
            if (
                string.Equals(
                    fileDirectory,
                    normalizedDirectory,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                var fileName = Path.GetFileName(filePath);
                if (pattern == "*")
                {
                    result.Add(filePath);
                }
                else if (pattern.Contains("*"))
                {
                    var patternWithoutStar = pattern.Replace("*", "");
                    if (fileName.Contains(patternWithoutStar))
                    {
                        result.Add(filePath);
                    }
                }
                else if (fileName == pattern)
                {
                    result.Add(filePath);
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Removes the file and its associated metadata from the in-memory store at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to delete. The path can be relative or absolute.</param>
    public void DeleteFile(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        _files.TryRemove(normalizedPath, out _);
    }

    /// <summary>
    /// Removes all files and their associated timestamps from the collection.
    /// </summary>
    public void Clear() => _files.Clear();
}
