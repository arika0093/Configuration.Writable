#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileWriter;

/// <summary>
/// Provides functionality to write data to a file, ensuring thread safety and data integrity.
/// </summary>
public class CommonFileWriter : IFileWriter, IDisposable
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
        CancellationToken cancellationToken = default,
        ILogger? logger = null
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
        var backupFilesOrderByCreated = Directory
            .GetFiles(Path.GetDirectoryName(path)!, "*.bak")
            .Select(f => new FileInfo(f))
            .Where(f => f.Name.StartsWith(Path.GetFileNameWithoutExtension(path)))
            .OrderBy(f => f.CreationTimeUtc)
            .ToList();

        logger?.LogTrace(
            "Found {BackupFileCount} backup files for {FilePath}",
            backupFilesOrderByCreated.Count,
            path
        );
        if (backupFilesOrderByCreated.Count >= BackupMaxCount)
        {
            // delete oldest files
            var deleteCount = backupFilesOrderByCreated.Count - BackupMaxCount + 1;
            foreach (var file in backupFilesOrderByCreated.Take(deleteCount))
            {
                logger?.LogDebug("Deleting old backup file: {BackupFilePath}", file.FullName);
                file.Delete();
            }
        }
        // create backup file
        var backupFilePath = GetTemporaryFilePath(path) + ".bak";
        logger?.LogDebug("Creating backup file for: {FilePath}", backupFilePath);
        File.Copy(path, backupFilePath);
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
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="CommonFileWriter"/> instance.
    /// </summary>
    protected virtual void Dispose(bool disposing) => _semaphore.Dispose();
}
