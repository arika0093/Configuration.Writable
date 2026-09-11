#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>
/// Provides functionality to write data to a file, ensuring thread safety and data integrity.
/// </summary>
public class CommonFileProvider
    : IWritableFileProvider,
        IBackupFileProvider,
        IFileDeleter,
        IPhysicalFileProvider,
        IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Gets or sets the maximum number of backup files to keep in the backup directory. Defaults to 1.
    /// </summary>
    public virtual int BackupMaxCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the backup directory relative to the configuration file. Use "/" to save backups alongside the
    /// configuration file. Defaults to "backup" on Windows or ".backup" on other platforms.
    /// </summary>
    public virtual string BackupDirectory { get; set; } =
        Path.DirectorySeparatorChar == '\\' ? "backup" : ".backup";

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
            logger?.LogTrace("Attempt {Attempt} to write file: {Path}", retryCount + 1, path);
            var shouldRetry = false;
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
                        logger?.LogDebug("Replacing original file: {Path}", path);
                        File.Replace(temporaryFilePath, path, null);
                    }
                    else
                    {
                        logger?.LogDebug("Moving temporary file to: {Path}", path);
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
                    "Failed to write file on attempt {Attempt}: {Path}",
                    retryCount + 1,
                    path
                );
                lastException = ex;
                retryCount++;
                shouldRetry = retryCount < MaxRetryCount;
            }
            finally
            {
                _semaphore.Release();
            }
            if (shouldRetry)
            {
                await Task.Delay(RetryDelay(retryCount), cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc />
    public virtual bool TryBackup(string path, out string? backupPath, ILogger? logger = null)
    {
        _semaphore.Wait();
        try
        {
            backupPath = CreateBackupFile(path, logger);
            return backupPath != null;
        }
        catch (IOException ex)
        {
            logger?.LogError(ex, "Failed to create configuration backup: {Path}", path);
            backupPath = null;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogError(ex, "Failed to create configuration backup: {Path}", path);
            backupPath = null;
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Generates a backup file before replacing the configuration file.
    /// </summary>
    /// <param name="path">The full path of the file to back up.</param>
    /// <param name="logger">An optional logger for backup diagnostics.</param>
    protected virtual void GenerateBackupFile(string path, ILogger? logger)
    {
        CreateBackupFile(path, logger);
    }

    private string? CreateBackupFile(string path, ILogger? logger)
    {
        if (!File.Exists(path))
        {
            logger?.LogTrace("File does not exist, skipping backup: {Path}", path);
            return null;
        }
        if (BackupMaxCount == 0)
        {
            logger?.LogTrace("BackupMaxCount is 0, skipping backup: {Path}", path);
            return null;
        }

        var backupFilesOrderByCreated = GetBackupFiles(path)
            .OrderBy(file => file.CreationTimeUtc)
            .ToList();

        logger?.LogTrace(
            "Found {BackupFileCount} backup files for {Path}",
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
        var backupDirectory = GetBackupDirectory(path);
        Directory.CreateDirectory(backupDirectory);
        SetHiddenOnWindows(backupDirectory);

        var backupFilePath = Path.Combine(backupDirectory, GetBackupFileName(path));
        logger?.LogDebug("Creating backup file for: {BackupFilePath}", backupFilePath);
        File.Copy(path, backupFilePath);
        SetHiddenOnWindows(backupFilePath);
        return backupFilePath;
    }

    private IEnumerable<FileInfo> GetBackupFiles(string path)
    {
        var backupDirectory = GetBackupDirectory(path);
        if (!Directory.Exists(backupDirectory))
        {
            return [];
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        if (Path.DirectorySeparatorChar != '\\')
        {
            fileNameWithoutExtension = "." + fileNameWithoutExtension;
        }
        var extension = Path.GetExtension(path);
        var backupPattern = $"{fileNameWithoutExtension}_*{extension}.bak";
        return Directory
            .GetFiles(backupDirectory, backupPattern)
            .Select(file => new FileInfo(file));
    }

    private string GetBackupDirectory(string path)
    {
        var configurationDirectory = Path.GetDirectoryName(path);
        configurationDirectory = string.IsNullOrEmpty(configurationDirectory)
            ? Directory.GetCurrentDirectory()
            : configurationDirectory;
        if (BackupDirectory == "/")
        {
            return configurationDirectory;
        }

        return Path.IsPathRooted(BackupDirectory)
            ? BackupDirectory
            : Path.Combine(configurationDirectory, BackupDirectory);
    }

    private string GetBackupFileName(string path)
    {
        var backupFileName = Path.GetFileName(GetTemporaryFilePath(path)) + ".bak";
        return Path.DirectorySeparatorChar == '\\' ? backupFileName : "." + backupFileName;
    }

    private static void SetHiddenOnWindows(string path)
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        }
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
        return WriteContentToFileWithStreamAsync(path, content, cancellationToken);
#else
        return Task.Run(() => File.WriteAllBytes(path, content.ToArray()), cancellationToken);
#endif
    }

#if NET8_0_OR_GREATER && !NET9_0_OR_GREATER
    private static async Task WriteContentToFileWithStreamAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }
#endif

    /// <inheritdoc />
    public virtual bool FileExists(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return File.Exists(normalizedPath);
    }

    bool IFileDeleter.TryDelete(string path, ILogger? logger)
    {
        try
        {
            File.Delete(Path.GetFullPath(path));
            return true;
        }
        catch (IOException ex)
        {
            logger?.LogWarning(ex, "Failed to delete file: {Path}", path);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogWarning(ex, "Failed to delete file: {Path}", path);
            return false;
        }
    }

    string IPhysicalFileProvider.GetPhysicalFilePath(string path) => Path.GetFullPath(path);

    /// <inheritdoc />
    public virtual PipeReader? GetFilePipeReader(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (!File.Exists(normalizedPath))
        {
            return null;
        }
        // Use FileShare.ReadWrite to allow concurrent access from FileSystemWatcher
        var stream = new FileStream(
            normalizedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        // Create a PipeReader from the stream for more efficient reading
        return PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: false));
    }

    /// <summary>
    /// Restores the most recent backup for a configuration file.
    /// </summary>
    /// <param name="path">The path of the configuration file to restore.</param>
    /// <param name="logger">An optional logger for restoration diagnostics.</param>
    /// <returns><see langword="true"/> when a backup was restored; otherwise <see langword="false"/>.</returns>
    public virtual bool TryRestoreLatestBackup(string path, ILogger? logger = null)
    {
        var backupFilePath = GetBackupFiles(path)
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
        if (backupFilePath == null)
        {
            return false;
        }

        var temporaryFilePath = GetTemporaryFilePath(path);
        try
        {
            File.Copy(backupFilePath, temporaryFilePath);
            if (File.Exists(path))
            {
                var corruptFilePath = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                File.Replace(temporaryFilePath, path, corruptFilePath);
            }
            else
            {
                File.Move(temporaryFilePath, path);
            }
            logger?.LogWarning(
                "Restored configuration from backup: {BackupFilePath}",
                backupFilePath
            );
            return true;
        }
        catch (IOException ex)
        {
            logger?.LogError(
                ex,
                "Failed to restore configuration backup: {BackupFilePath}",
                backupFilePath
            );
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogError(
                ex,
                "Failed to restore configuration backup: {BackupFilePath}",
                backupFilePath
            );
            return false;
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    /// <inheritdoc />
    public virtual bool DirectoryExists(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return Directory.Exists(normalizedPath);
    }

    /// <inheritdoc />
    public virtual bool CanWriteToFile(string path)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(path);
            if (!File.Exists(normalizedPath))
            {
                return false;
            }

            using var stream = File.Open(normalizedPath, FileMode.Open, FileAccess.Write);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public virtual bool CanWriteToDirectory(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path) ?? "";
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

    /// <inheritdoc />
    public virtual bool EnsureDirectoryExists(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
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
