using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

public class CommonFileProviderTests
{
    private static Task<byte[]> ReadAllBytesCompat(string path)
    {
#if NETFRAMEWORK
        return Task.Run(() => File.ReadAllBytes(path));
#else
        return File.ReadAllBytesAsync(path);
#endif
    }

    private static Task<string> ReadAllTextCompat(string path)
    {
#if NETFRAMEWORK
        return Task.Run(() => File.ReadAllText(path));
#else
        return File.ReadAllTextAsync(path);
#endif
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldCreateFileWithContent()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider();
        var content = Encoding.UTF8.GetBytes("Hello, World!");

        await writer.SaveToFileAsync(testFile.FilePath, content);

        File.Exists(testFile.FilePath).ShouldBeTrue();
        var savedContent = await ReadAllBytesCompat(testFile.FilePath);
        savedContent.ShouldBe(content);
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldCreateDirectoryIfNotExists()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var testFile = new TemporaryFile(testDir, "test.txt");
        var writer = new CommonFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        Directory.Exists(testDir).ShouldBeFalse();

        await writer.SaveToFileAsync(testFile.FilePath, content);

        Directory.Exists(testDir).ShouldBeTrue();
        File.Exists(testFile.FilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldReplaceExistingFile()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider();
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        // Create original file
        await writer.SaveToFileAsync(testFile.FilePath, originalContent);
        var firstSave = await ReadAllBytesCompat(testFile.FilePath);
        firstSave.ShouldBe(originalContent);

        // Replace with new content
        await writer.SaveToFileAsync(testFile.FilePath, newContent);
        var secondSave = await ReadAllBytesCompat(testFile.FilePath);
        secondSave.ShouldBe(newContent);
    }

    [Fact]
    public async Task SaveToFileAsync_WithBackup_ShouldCreateBackupFile()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider { BackupMaxCount = 3 };
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        var directory = Path.GetDirectoryName(testFile.FilePath)!;
        var backupPattern = $"{testFile.FileName.Split('.')[0]}_*.bak";

        // Create original file
        await writer.SaveToFileAsync(testFile.FilePath, originalContent);

        // Update file (should create backup)
        await writer.SaveToFileAsync(testFile.FilePath, newContent);

        // Check that at least one backup file was created
        var backupFiles = Directory.GetFiles(directory, backupPattern);
        backupFiles.Length.ShouldBeGreaterThanOrEqualTo(1); // in .NET FW, sometime two files created due to timing

        // Verify current file content
        var currentContent = await ReadAllBytesCompat(testFile.FilePath);
        currentContent.ShouldBe(newContent);
    }

    [Fact]
    public async Task SaveToFileAsync_WithBackupMaxCount_ShouldLimitBackupFiles()
    {
        // Use a unique subdirectory to isolate test files
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            using var testFile = new TemporaryFile(testDir, $"{Guid.NewGuid():N}.sample");
            var writer = new CommonFileProvider { BackupMaxCount = 2 };

            // Create multiple versions to exceed backup limit
            for (int i = 0; i < 10; i++)
            {
                var content = Encoding.UTF8.GetBytes($"Content version {i}");
                await writer.SaveToFileAsync(testFile.FilePath, content);

                // Add small delay to ensure different timestamps and avoid error
                await Task.Delay(200);
            }

            // Check that backup files are limited (may be slightly more due to timing)
            var directory = Path.GetDirectoryName(testFile.FilePath)!;
            var backupPattern = $"*.bak";
            var backupFiles = Directory.GetFiles(directory, backupPattern);
            backupFiles.Length.ShouldBe(2);
        }
        finally
        {
            // Clean up test directory
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public async Task SaveToFileAsync_WithZeroBackupCount_ShouldNotCreateBackups()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider { BackupMaxCount = 0 };
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        // Create and update file
        await writer.SaveToFileAsync(testFile.FilePath, originalContent);
        await writer.SaveToFileAsync(testFile.FilePath, newContent);

        // Check that no backup files were created
        var directory = Path.GetDirectoryName(testFile.FilePath)!;
        var backupFiles = Directory.GetFiles(directory, "*.bak");
        backupFiles.Length.ShouldBe(0);
    }

    [Fact]
    public async Task SaveToFileAsync_ConcurrentWrites_ShouldBeThreadSafe()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider();
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            var content = Encoding.UTF8.GetBytes($"Content {i}");
            tasks[i] = writer.SaveToFileAsync(testFile.FilePath, content);
        }

        await Task.WhenAll(tasks);

        // File should exist and contain one of the written contents
        File.Exists(testFile.FilePath).ShouldBeTrue();
        var finalContent = await ReadAllTextCompat(testFile.FilePath);
        finalContent.ShouldStartWith("Content ");
    }

    [Fact]
    public async Task SaveToFileAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await writer.SaveToFileAsync(testFile.FilePath, content, cancellationToken: cts.Token);
            // If we reach here, cancellation was not respected
            throw new Exception("Operation was not cancelled as expected.");
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation is respected
        }
    }

    [Fact]
    public async Task SaveToFileAsync_WithEmptyContent_ShouldCreateEmptyFile()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider();
        var emptyContent = ReadOnlyMemory<byte>.Empty;

        await writer.SaveToFileAsync(testFile.FilePath, emptyContent);

        File.Exists(testFile.FilePath).ShouldBeTrue();
        var fileInfo = new FileInfo(testFile.FilePath);
        fileInfo.Length.ShouldBe(0);
    }

    [Fact]
    public async Task SaveToFileAsync_WithLargeContent_ShouldHandleLargeFiles()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileProvider();

        // Create 1MB of content
        var largeContent = new byte[1024 * 1024];
        new Random().NextBytes(largeContent);

        await writer.SaveToFileAsync(testFile.FilePath, largeContent);

        File.Exists(testFile.FilePath).ShouldBeTrue();
        var savedContent = await ReadAllBytesCompat(testFile.FilePath);
        savedContent.ShouldBe(largeContent);
    }
}
