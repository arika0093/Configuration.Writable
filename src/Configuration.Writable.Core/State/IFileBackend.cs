using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.State;

/// <summary>
/// Internal file system backend for file-backed state resources. Replaces the
/// former public file-provider abstractions; production code uses
/// <see cref="PhysicalFileBackend"/>, tests may substitute an in-memory backend.
/// </summary>
internal interface IFileBackend
{
    bool IsPhysical { get; }

    string GetPhysicalPath(string path);

    bool FileExists(string path);

    Stream? OpenReadStream(string path);

    Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    );

    bool DirectoryExists(string path);

    bool CanWriteToFile(string path);

    bool CanWriteToDirectory(string path);

    bool EnsureDirectoryExists(string path);

    bool TryBackup(string path, out string? backupPath, ILogger? logger = null);

    bool TryDelete(string path, ILogger? logger = null);

    bool TryRestoreLatestBackup(string path, ILogger? logger = null);
}
