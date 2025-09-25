#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Internal;

namespace Configuration.Writable.FileWriter;

/// <summary>
/// Provides functionality to write data to a file, ensuring thread safety and data integrity.
/// </summary>
public class CommonFileWriter : IFileWriter
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
        CancellationToken cancellationToken
    )
    {
        int retryCount = 0;
        Exception? lastException = null;
        while (retryCount < MaxRetryCount)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // Create directory if it does not exist
                var directory = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(directory);

                // Generate backup file
                GenerateBackupFile(path);

                string temporaryFilePath = GetTemporaryFilePath(path);
                using (new TemporaryFile(temporaryFilePath))
                {
                    // Write to temporary file first
                    await WriteContentToFileAsync(temporaryFilePath, content, cancellationToken);
                    // Replace original file
                    if (File.Exists(path))
                    {
                        File.Replace(temporaryFilePath, path, null);
                    }
                    else
                    {
                        File.Move(temporaryFilePath, path);
                    }
                    // Exit if successful
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                retryCount++;
                // Wait 100ms before retrying
                var delayMs = RetryDelay(retryCount);
                await Task.Delay(delayMs, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        // throw last exception
        if (lastException != null)
        {
            throw lastException;
        }
    }

    /// <summary>
    /// Generates a unique temporary file path based on the specified file path.
    /// </summary>
    /// <param name="path">The original file path to use as a base for generating the temporary file path. Must not be null and must
    /// include a file name.</param>
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
    protected virtual void GenerateBackupFile(string path)
    {
        // if file does not exist, do nothing
        if (!File.Exists(path))
        {
            return;
        }
        // if backup count is 0, do nothing
        if (BackupMaxCount == 0)
        {
            return;
        }
        // delete older backup files
        var backupFilesOrderByCreated = Directory
            .GetFiles(Path.GetDirectoryName(path)!, "*.bak")
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.CreationTimeUtc)
            .ToList();
        if (backupFilesOrderByCreated.Count >= BackupMaxCount)
        {
            // delete oldest files
            var deleteCount = backupFilesOrderByCreated.Count - BackupMaxCount + 1;
            foreach (var file in backupFilesOrderByCreated.Take(deleteCount))
            {
                file.Delete();
            }
        }
        // create backup file
        var backupFilePath = GetTemporaryFilePath(path) + ".bak";
        File.Copy(path, backupFilePath);
    }

    // write content to file
    private static Task WriteContentToFileAsync(
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
}
