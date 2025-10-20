using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>
/// Provides functionality to write data to a zip file. support multiple file entries.
/// </summary>
public class ZipFileProvider : IFileProvider, IDisposable
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
    public Stream? GetFileStream(string path)
    {
        var zip = GetZipEntry(path, out var entry);
        if (zip == null || entry == null)
        {
            return null;
        }
        return new DisposeStream(entry.Open(), zip.Dispose);
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
#if NET10_0_OR_GREATER
            using var zip = await ZipFile
                .OpenAsync(zipPath, ZipArchiveMode.Update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
#else
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update);
#endif
            var entryPath = GetZipInnerEntryPath(path);
            var entry = zip.GetEntry(entryPath);
            entry?.Delete();
            entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
#if NET10_0_OR_GREATER
            using var entryStream = await entry.OpenAsync(cancellationToken);
#else
            using var entryStream = entry.Open();
#endif
#if NET8_0_OR_GREATER
            await entryStream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
#else
            await entryStream
                .WriteAsync(content.ToArray(), 0, content.Length, cancellationToken)
                .ConfigureAwait(false);
#endif
        }
        finally
        {
            _semaphore.Release();
        }
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
