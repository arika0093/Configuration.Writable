#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>
/// Provides functionality to write data to a file, ensuring thread safety and data integrity.
/// </summary>
public class CommonFileProvider : IFileProvider, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Gets or sets the maximum number of backup files to keep. Defaults to 0.
    /// </summary>
    public virtual int BackupMaxCount { get; set; } = 0;

    /// <summary>
    /// The maximum number of retry attempts when a file write operation fails due to an exception. Defaults to 3.
    /// </summary>
    public virtual int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// A function that provides the delay in milliseconds before each retry attempt based on the current retry attempt number.
    /// Defaults to a function that returns 100 milliseconds for any attempt.
    /// </summary>
    public virtual Func<int, int> RetryDelay { get; set; } = retryAttempt => 100;

    /// <inheritdoc />
    public virtual async Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        int retryCount = 0;
        Exception? lastException = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger?.LogTrace(
                "Attempt {RetryCount} to write file: {FilePath}",
                retryCount + 1,
                path
            );
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Create directory if it does not exist
                var directory = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(directory))
                {
                    logger?.LogTrace("Creating directory: {Directory}", directory);
                    Directory.CreateDirectory(directory);
                    logger?.LogTrace("Directory created: {Directory}", directory);
                }

                // Generate backup file
                GenerateBackupFile(path, logger);

                string temporaryFilePath = GetTemporaryFilePath(path);
                using (new TemporaryFile(temporaryFilePath))
                {
                    logger?.LogDebug(
                        "Writing to temporary file: {TemporaryFilePath}",
                        temporaryFilePath
                    );
                    // Write to temporary file first
                    await WriteContentToFileAsync(temporaryFilePath, content, cancellationToken)
                        .ConfigureAwait(false);
                    // Replace original file
                    if (File.Exists(path))
                    {
                        logger?.LogDebug("Replacing original file: {FilePath}", path);
                        File.Replace(temporaryFilePath, path, null);
                    }
                    else
                    {
                        logger?.LogDebug("Moving temporary file to: {FilePath}", path);
                        File.Move(temporaryFilePath, path);
                    }
                    // Exit if successful
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    ex,
                    "Failed to write file on attempt {RetryCount}: {FilePath}",
                    retryCount + 1,
                    path
                );
                lastException = ex;
                retryCount++;
                // Wait delay before retrying
                var delayMs = RetryDelay(retryCount);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        } while (retryCount < MaxRetryCount);
        throw lastException;
    }

    /// <summary>
    /// Generates a unique temporary file path based on the specified file path.
    /// </summary>
    /// <param name="path">The original file path to use as a base for generating the temporary file path. Cannot be null and must
    /// contain a file name.</param>
    protected virtual string GetTemporaryFilePath(string path)
    {
        var extension = Path.GetExtension(path);
        var filePathWithoutExtension = Path.Combine(
            Path.GetDirectoryName(path)!,
            Path.GetFileNameWithoutExtension(path)
        );
        var timestamp = DateTime.UtcNow.Ticks;
        return $"{filePathWithoutExtension}_{timestamp}{extension}";
    }

    /// <summary>
    /// Creates a backup of the specified file if it exists and manages the number of backup files according to the
    /// maximum backup count.
    /// </summary>
    /// <param name="path">The full path of the file to back up. Must not be null or empty.</param>
    /// <param name="logger">An optional logger for logging operations and errors.</param>
    protected virtual void GenerateBackupFile(string path, ILogger? logger)
    {
        // if file does not exist, do nothing
        if (!File.Exists(path))
        {
            logger?.LogTrace("File does not exist, skipping backup: {FilePath}", path);
            return;
        }
        // if backup count is 0, do nothing
        if (BackupMaxCount == 0)
        {
            logger?.LogTrace("BackupMaxCount is 0, skipping backup: {FilePath}", path);
            return;
        }
        // delete older backup files
        var baseFileName = Path.GetFileNameWithoutExtension(path);
        var backupFilesOrderByTimestamp = Directory
            .GetFiles(Path.GetDirectoryName(path)!, "*.bak")
            .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(baseFileName + "_"))
            .Select(f => new
            {
                FilePath = f,
                Timestamp = ExtractTimestampFromBackupFilename(f, baseFileName),
            })
            .Where(f => f.Timestamp.HasValue)
            .OrderBy(f => f.Timestamp!.Value)
            .ToList();

        logger?.LogTrace(
            "Found {BackupFileCount} backup files for {FilePath}",
            backupFilesOrderByTimestamp.Count,
            path
        );
        if (backupFilesOrderByTimestamp.Count >= BackupMaxCount)
        {
            // delete oldest files to make room for the new backup
            var deleteCount = backupFilesOrderByTimestamp.Count - (BackupMaxCount - 1);
            var filesToDelete = backupFilesOrderByTimestamp
                .Take(deleteCount)
                .Select(f => f.FilePath);
            foreach (var filePath in filesToDelete)
            {
                logger?.LogDebug("Deleting old backup file: {BackupFilePath}", filePath);
                File.Delete(filePath);
            }
        }
        // create backup file
        var backupFilePath = GetTemporaryFilePath(path) + ".bak";
        logger?.LogDebug("Creating backup file for: {FilePath}", backupFilePath);
        File.Copy(path, backupFilePath);
    }

    /// <summary>
    /// Extracts the timestamp from a backup filename.
    /// </summary>
    /// <param name="backupFilePath">The full path to the backup file.</param>
    /// <param name="baseFileName">The base file name without extension.</param>
    /// <returns>The timestamp in ticks if successfully extracted; otherwise, null.</returns>
    private static long? ExtractTimestampFromBackupFilename(
        string backupFilePath,
        string baseFileName
    )
    {
        try
        {
            // Backup file format: {baseFileName}_{timestamp}{originalExtension}.bak
            // Example: abc123_638123456789.sample.bak
            // Path.GetFileNameWithoutExtension removes .bak -> abc123_638123456789.sample
            var fileNameWithoutBakExt = Path.GetFileNameWithoutExtension(backupFilePath);

            // Check if it starts with baseFileName + "_"
            var prefix = baseFileName + "_";
            if (!fileNameWithoutBakExt.StartsWith(prefix))
            {
                return null;
            }

            // Extract the part after prefix: "638123456789.sample"
            var remainder = fileNameWithoutBakExt.Substring(prefix.Length);

            // Find the first dot or end of string to extract timestamp
            var dotIndex = remainder.IndexOf('.');
            var timestampStr = dotIndex >= 0 ? remainder.Substring(0, dotIndex) : remainder;

            if (long.TryParse(timestampStr, out var timestamp))
            {
                return timestamp;
            }
        }
        catch
        {
            // Ignore parse errors
        }
        return null;
    }

    /// <summary>
    /// Writes the specified content to a file asynchronously.
    /// </summary>
    /// <param name="path">The full path of the file to write to. Must not be null or empty.</param>
    /// <param name="content">The content to write to the file as a read-only memory of bytes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    protected virtual Task WriteContentToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
#if NET9_0_OR_GREATER
        return File.WriteAllBytesAsync(path, content, cancellationToken);
#elif NET
        return File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
#else
        return Task.Run(() => File.WriteAllBytes(path, content.ToArray()), cancellationToken);
#endif
    }

    /// <inheritdoc />
    public virtual bool FileExists(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return File.Exists(normalizedPath);
    }

    /// <inheritdoc />
    public virtual Stream? GetFileStream(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (!File.Exists(normalizedPath))
        {
            return null;
        }
        // Use FileShare.ReadWrite to allow concurrent access from FileSystemWatcher
        return new FileStream(
            normalizedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: false
        );
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
