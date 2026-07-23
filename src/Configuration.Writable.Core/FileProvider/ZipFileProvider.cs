using System;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>
/// Provides functionality to write data to a zip file. support multiple file entries.
/// </summary>
public class ZipFileProvider : IWritableFileProvider, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Gets or sets the name of the zip file. Defaults to "config.zip".
    /// </summary>
    public string ZipFileName { get; set; } = "config.zip";

    /// <summary>
    /// Gets or sets the directory inside the zip file where entries are stored. Defaults to "/".
    /// </summary>
    public string EntriesDirectory { get; set; } = "/";

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        using var zip = GetZipEntry(path, out var entry);
        return entry != null;
    }

    /// <inheritdoc/>
    public PipeReader? GetFilePipeReader(string path)
    {
        var zip = GetZipEntry(path, out var entry);
        if (zip == null || entry == null)
        {
            return null;
        }
        var stream = new DisposeStream(entry.Open(), zip.Dispose);
        // Create a PipeReader from the stream for more efficient reading
        return PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: false));
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        var zipPath = GetZipFilePath(path);
        var directory = Path.GetDirectoryName(zipPath);
        return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
    }

    /// <inheritdoc/>
    public bool CanWriteToFile(string path)
    {
        try
        {
            var zipPath = GetZipFilePath(path);
            if (!File.Exists(zipPath))
            {
                return false;
            }

            using var stream = File.Open(zipPath, FileMode.Open, FileAccess.Write);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool CanWriteToDirectory(string path)
    {
        try
        {
            var zipPath = GetZipFilePath(path);
            var directory = Path.GetDirectoryName(zipPath) ?? "";
            if (!Directory.Exists(directory))
            {
                return false;
            }

            var testFilePath = Path.Combine(directory, Path.GetRandomFileName());
            using (File.Create(testFilePath, 1, FileOptions.DeleteOnClose))
            {
                // No action needed here as the file will be deleted on close
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool EnsureDirectoryExists(string path)
    {
        try
        {
            var zipPath = GetZipFilePath(path);
            var fullPath = Path.GetFullPath(zipPath);
            var directory = Path.GetDirectoryName(fullPath);
            // If no directory is specified (relative filename like "file.json"),
            // default to the current directory
            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Verify write access by creating and deleting a temporary file
            var testFilePath = Path.Combine(directory, Path.GetRandomFileName());
            using (File.Create(testFilePath, 1, FileOptions.DeleteOnClose))
            {
                // No action needed here as the file will be deleted on close
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        // Ensure thread-safe access to the zip file.
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var zipPath = GetZipFilePath(path);

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entryPath = GetZipInnerEntryPath(path);
            var temporaryZipPath = $"{zipPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await CreateUpdatedArchiveAsync(
                        zipPath,
                        temporaryZipPath,
                        entryPath,
                        content,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (File.Exists(zipPath))
                {
                    File.Replace(temporaryZipPath, zipPath, null);
                }
                else
                {
                    File.Move(temporaryZipPath, zipPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryZipPath))
                {
                    File.Delete(temporaryZipPath);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async Task CreateUpdatedArchiveAsync(
        string sourceZipPath,
        string destinationZipPath,
        string replacementEntryPath,
        ReadOnlyMemory<byte> replacementContent,
        CancellationToken cancellationToken
    )
    {
        using var destinationStream = new FileStream(
            destinationZipPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        using var destinationArchive = new ZipArchive(
            destinationStream,
            ZipArchiveMode.Create,
            leaveOpen: true
        );

        if (File.Exists(sourceZipPath))
        {
            using var sourceArchive = ZipFile.OpenRead(sourceZipPath);
            foreach (var sourceEntry in sourceArchive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (
                    sourceEntry.FullName.Equals(
                        replacementEntryPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                var destinationEntry = destinationArchive.CreateEntry(
                    sourceEntry.FullName,
                    CompressionLevel.Optimal
                );
                destinationEntry.LastWriteTime = sourceEntry.LastWriteTime;
                using var sourceStream = sourceEntry.Open();
                using var destinationEntryStream = destinationEntry.Open();
                await sourceStream
                    .CopyToAsync(destinationEntryStream, 81920, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var replacementEntry = destinationArchive.CreateEntry(
            replacementEntryPath,
            CompressionLevel.Optimal
        );
        using var replacementStream = replacementEntry.Open();
#if NET8_0_OR_GREATER
        await replacementStream
            .WriteAsync(replacementContent, cancellationToken)
            .ConfigureAwait(false);
#else
        await replacementStream
            .WriteAsync(
                replacementContent.ToArray(),
                0,
                replacementContent.Length,
                cancellationToken
            )
            .ConfigureAwait(false);
#endif
    }

    // Gets the full path to the zip file based on the original file path.
    private string GetZipFilePath(string originalPath)
    {
        // replace filename
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        return Path.Combine(directory, ZipFileName);
    }

    // Gets the path of the zip file for the given original file path.
    private string GetZipInnerEntryPath(string originalPath)
    {
        var fileName = Path.GetFileName(originalPath);
        return Path.Combine(EntriesDirectory.TrimStart('/'), fileName).Replace('\\', '/');
    }

    // Retrieves the zip archive and the specific entry for the given original file path.
    private ZipArchive? GetZipEntry(string originalPath, out ZipArchiveEntry? entry)
    {
        var zipPath = GetZipFilePath(originalPath);
        if (!File.Exists(zipPath))
        {
            entry = null;
            return null;
        }

        var zip = ZipFile.OpenRead(zipPath);
        var entryPath = GetZipInnerEntryPath(originalPath);
        entry = zip.Entries.FirstOrDefault(e =>
            e.FullName.Equals(entryPath, StringComparison.OrdinalIgnoreCase)
        );
        if (entry == null)
        {
            zip.Dispose();
            return null;
        }
        return zip;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="CommonFileProvider"/> instance.
    /// </summary>
    protected virtual void Dispose(bool disposing) => _semaphore.Dispose();
}

// A helper class to dispose a stream with a custom action.
internal class DisposeStream(Stream stream, Action onDispose) : Stream
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        onDispose();
    }

    // Delegate all other Stream members to the underlying stream

    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => stream.CanSeek;

    public override bool CanWrite => stream.CanWrite;

    public override long Length => stream.Length;

    public override long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }

    public override void Flush() => stream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value) => stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        stream.Write(buffer, offset, count);
}
