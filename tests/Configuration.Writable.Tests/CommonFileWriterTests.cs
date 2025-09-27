using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

public class CommonFileWriterTests
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
        var writer = new CommonFileWriter();
        var content = Encoding.UTF8.GetBytes("Hello, World!");

        await writer.SaveToFileAsync(testFile.FilePath, content, CancellationToken.None);

        File.Exists(testFile.FilePath).ShouldBeTrue();
        var savedContent = await ReadAllBytesCompat(testFile.FilePath);
        savedContent.ShouldBe(content);
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldCreateDirectoryIfNotExists()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var testFile = new TemporaryFile(testDir, "test.txt");
        var writer = new CommonFileWriter();
        var content = Encoding.UTF8.GetBytes("Test content");

        Directory.Exists(testDir).ShouldBeFalse();

        await writer.SaveToFileAsync(testFile.FilePath, content, CancellationToken.None);

        Directory.Exists(testDir).ShouldBeTrue();
        File.Exists(testFile.FilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldReplaceExistingFile()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileWriter();
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        // Create original file
        await writer.SaveToFileAsync(testFile.FilePath, originalContent, CancellationToken.None);
        var firstSave = await ReadAllBytesCompat(testFile.FilePath);
        firstSave.ShouldBe(originalContent);

        // Replace with new content
        await writer.SaveToFileAsync(testFile.FilePath, newContent, CancellationToken.None);
        var secondSave = await ReadAllBytesCompat(testFile.FilePath);
        secondSave.ShouldBe(newContent);
    }

    [Fact]
    public async Task SaveToFileAsync_WithBackup_ShouldCreateBackupFile()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileWriter { BackupMaxCount = 3 };
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        // remove any existing backup files
        var directory = Path.GetDirectoryName(testFile.FilePath)!;
        var backupPattern = $"{testFile.FileName.Split('.')[0]}_*.bak";
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory, backupPattern))
            {
                File.Delete(file);
            }
        }

        // Create original file
        await writer.SaveToFileAsync(testFile.FilePath, originalContent, CancellationToken.None);

        // Update file (should create backup)
        await writer.SaveToFileAsync(testFile.FilePath, newContent, CancellationToken.None);

        // Check that at least one backup file was created
        var backupFiles = Directory.GetFiles(directory, backupPattern);
        backupFiles.Length.ShouldBe(1);

        // Verify current file content
        var currentContent = await ReadAllBytesCompat(testFile.FilePath);
        currentContent.ShouldBe(newContent);
    }

    [Fact]
    public async Task SaveToFileAsync_WithBackupMaxCount_ShouldLimitBackupFiles()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileWriter { BackupMaxCount = 2 };

        // Create multiple versions to exceed backup limit
        for (int i = 0; i < 5; i++)
        {
            var content = Encoding.UTF8.GetBytes($"Content version {i}");
            await writer.SaveToFileAsync(testFile.FilePath, content, CancellationToken.None);

            // Add small delay to ensure different timestamps
            await Task.Delay(50);
        }

        // Check that backup files are limited (may be slightly more due to timing)
        var directory = Path.GetDirectoryName(testFile.FilePath)!;
        var backupPattern = $"{testFile.FileName.Split('.')[0]}_*.bak";
        var backupFiles = Directory.GetFiles(directory, backupPattern);
        backupFiles.Length.ShouldBe(2);
    }

    [Fact]
    public async Task SaveToFileAsync_WithZeroBackupCount_ShouldNotCreateBackups()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileWriter { BackupMaxCount = 0 };
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        // Create and update file
        await writer.SaveToFileAsync(testFile.FilePath, originalContent, CancellationToken.None);
        await writer.SaveToFileAsync(testFile.FilePath, newContent, CancellationToken.None);

        // Check that no backup files were created
        var directory = Path.GetDirectoryName(testFile.FilePath)!;
        var backupFiles = Directory.GetFiles(directory, "*.bak");
        backupFiles.Length.ShouldBe(0);
    }

    [Fact]
    public async Task SaveToFileAsync_ConcurrentWrites_ShouldBeThreadSafe()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileWriter();
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            var content = Encoding.UTF8.GetBytes($"Content {i}");
            tasks[i] = writer.SaveToFileAsync(testFile.FilePath, content, CancellationToken.None);
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
        var writer = new CommonFileWriter();
        var content = Encoding.UTF8.GetBytes("Test content");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await writer.SaveToFileAsync(testFile.FilePath, content, cts.Token);
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
        var writer = new CommonFileWriter();
        var emptyContent = ReadOnlyMemory<byte>.Empty;

        await writer.SaveToFileAsync(testFile.FilePath, emptyContent, CancellationToken.None);

        File.Exists(testFile.FilePath).ShouldBeTrue();
        var fileInfo = new FileInfo(testFile.FilePath);
        fileInfo.Length.ShouldBe(0);
    }

    [Fact]
    public async Task SaveToFileAsync_WithLargeContent_ShouldHandleLargeFiles()
    {
        using var testFile = new TemporaryFile();
        var writer = new CommonFileWriter();

        // Create 1MB of content
        var largeContent = new byte[1024 * 1024];
        new Random().NextBytes(largeContent);

        await writer.SaveToFileAsync(testFile.FilePath, largeContent, CancellationToken.None);

        File.Exists(testFile.FilePath).ShouldBeTrue();
        var savedContent = await ReadAllBytesCompat(testFile.FilePath);
        savedContent.ShouldBe(largeContent);
    }
}
