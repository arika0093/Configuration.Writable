using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.FileWriter;

/// <summary>
/// Provides an in-memory implementation of the <see cref="IFileWriter"/> interface for managing files and directories without
/// persistent storage. for testing purposes.
/// </summary>
public class InMemoryFileWriter : IFileWriter
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    /// <inheritdoc />
    public Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
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

    /// <summary>
    /// Returns a stream for reading the contents of the specified file path. If the file does not exist, returns null.
    /// </summary>
    /// <param name="path">The path of the file to retrieve. Can be relative or absolute.</param>
    public Stream? GetFileStream(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (!_files.TryGetValue(normalizedPath, out var content))
        {
            return null;
        }
        return new MemoryStream(content);
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
