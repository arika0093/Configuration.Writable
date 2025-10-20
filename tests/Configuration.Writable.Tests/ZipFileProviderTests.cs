using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

public class ZipFileProviderTests
{
    private static Task<byte[]> ReadAllBytesCompat(string path)
    {
#if NETFRAMEWORK
        return Task.Run(() => File.ReadAllBytes(path));
#else
        return File.ReadAllBytesAsync(path);
#endif
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldCreateZipFileWithEntry()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var content = Encoding.UTF8.GetBytes("Hello, World!");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        File.Exists(zipPath).ShouldBeTrue();

        // Verify the entry exists in the zip file
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(Path.GetFileName(testFile.FilePath));
        entry.ShouldNotBeNull();

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var savedContent = await reader.ReadToEndAsync();
        savedContent.ShouldBe("Hello, World!");
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldUpdateExistingEntry()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");

        // Create original entry
        await provider.SaveToFileAsync(testFile.FilePath, originalContent);

        // Update entry
        await provider.SaveToFileAsync(testFile.FilePath, newContent);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(Path.GetFileName(testFile.FilePath));
        entry.ShouldNotBeNull();

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var savedContent = await reader.ReadToEndAsync();
        savedContent.ShouldBe("New content");
    }

    [Fact]
    public async Task SaveToFileAsync_WithMultipleFiles_ShouldCreateMultipleEntries()
    {
        using var testFile1 = new TemporaryFile();
        using var testFile2 = new TemporaryFile(
            Path.GetDirectoryName(testFile1.FilePath)!,
            "file2.json"
        );
        using var provider = new ZipFileProvider();

        var content1 = Encoding.UTF8.GetBytes("Content 1");
        var content2 = Encoding.UTF8.GetBytes("Content 2");

        await provider.SaveToFileAsync(testFile1.FilePath, content1);
        await provider.SaveToFileAsync(testFile2.FilePath, content2);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile1.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);
        zip.Entries.Count.ShouldBeGreaterThanOrEqualTo(2);

        var entry1 = zip.GetEntry(Path.GetFileName(testFile1.FilePath));
        var entry2 = zip.GetEntry(Path.GetFileName(testFile2.FilePath));
        entry1.ShouldNotBeNull();
        entry2.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveToFileAsync_WithCustomZipFileName_ShouldUseCustomName()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider { ZipFileName = "custom.zip" };
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var customZipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "custom.zip");
        File.Exists(customZipPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveToFileAsync_WithEntriesDirectory_ShouldCreateEntryInSubdirectory()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider { EntriesDirectory = "configs" };
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);
        var expectedEntryPath = $"configs/{Path.GetFileName(testFile.FilePath)}";
        var entry = zip.GetEntry(expectedEntryPath);
        entry.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveToFileAsync_WithEntriesDirectoryStartingWithSlash_ShouldCreateEntryInSubdirectory()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider { EntriesDirectory = "/configs" };
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);
        var expectedEntryPath = $"configs/{Path.GetFileName(testFile.FilePath)}";
        var entry = zip.GetEntry(expectedEntryPath);
        entry.ShouldNotBeNull();
    }

    [Fact]
    public async Task FileExists_WhenEntryExists_ShouldReturnTrue()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var exists = provider.FileExists(testFile.FilePath);
        exists.ShouldBeTrue();
    }

    [Fact]
    public void FileExists_WhenEntryDoesNotExist_ShouldReturnFalse()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();

        var exists = provider.FileExists(testFile.FilePath);
        exists.ShouldBeFalse();
    }

    [Fact]
    public void FileExists_WhenZipFileDoesNotExist_ShouldReturnFalse()
    {
        var testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "test.json");
        using var provider = new ZipFileProvider();

        var exists = provider.FileExists(testPath);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetFileStream_WhenEntryExists_ShouldReturnStream()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        using var stream = provider.GetFileStream(testFile.FilePath);
        stream.ShouldNotBeNull();

        using var reader = new StreamReader(stream);
        var readContent = await reader.ReadToEndAsync();
        readContent.ShouldBe("Test content");
    }

    [Fact]
    public void GetFileStream_WhenEntryDoesNotExist_ShouldReturnNull()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();

        var stream = provider.GetFileStream(testFile.FilePath);
        stream.ShouldBeNull();
    }

    [Fact]
    public void GetFileStream_WhenZipFileDoesNotExist_ShouldReturnNull()
    {
        var testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "test.json");
        using var provider = new ZipFileProvider();

        var stream = provider.GetFileStream(testPath);
        stream.ShouldBeNull();
    }

    [Fact]
    public async Task SaveToFileAsync_ConcurrentWrites_ShouldBeThreadSafe()
    {
        using var testDir = new TemporaryFile(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var directory = Path.GetDirectoryName(testDir.FilePath)!;
        Directory.CreateDirectory(directory);

        using var provider = new ZipFileProvider();
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            var filePath = Path.Combine(directory, $"file{i}.json");
            var content = Encoding.UTF8.GetBytes($"Content {i}");
            tasks[i] = provider.SaveToFileAsync(filePath, content);
        }

        await Task.WhenAll(tasks);

        var zipPath = Path.Combine(directory, "config.zip");
        File.Exists(zipPath).ShouldBeTrue();

        using var zip = ZipFile.OpenRead(zipPath);
        zip.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task SaveToFileAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await provider.SaveToFileAsync(
                testFile.FilePath,
                content,
                cancellationToken: cts.Token
            );
            throw new Exception("Operation was not cancelled as expected.");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    [Fact]
    public async Task SaveToFileAsync_WithEmptyContent_ShouldCreateEmptyEntry()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var emptyContent = ReadOnlyMemory<byte>.Empty;

        await provider.SaveToFileAsync(testFile.FilePath, emptyContent);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(Path.GetFileName(testFile.FilePath));
        entry.ShouldNotBeNull();
        entry.Length.ShouldBe(0);
    }

    [Fact]
    public async Task GetFileStream_AfterDispose_ShouldStillWork()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        using var stream = provider.GetFileStream(testFile.FilePath);
        stream.ShouldNotBeNull();

        // Dispose the stream
        stream.Dispose();

        // Should be able to get a new stream
        using var stream2 = provider.GetFileStream(testFile.FilePath);
        stream2.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveToFileAsync_MultipleTimes_ShouldOnlyKeepLatestVersion()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider();

        for (int i = 0; i < 5; i++)
        {
            var content = Encoding.UTF8.GetBytes($"Version {i}");
            await provider.SaveToFileAsync(testFile.FilePath, content);
        }

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);

        // Should only have one entry for the file
        var entries = zip
            .Entries.Where(e => e.Name == Path.GetFileName(testFile.FilePath))
            .ToList();
        entries.Count.ShouldBe(1);

        using var stream = entries[0].Open();
        using var reader = new StreamReader(stream);
        var readContent = await reader.ReadToEndAsync();
        readContent.ShouldBe("Version 4");
    }

    [Fact]
    public async Task Dispose_ShouldReleaseResources()
    {
        using var testFile = new TemporaryFile();
        var provider = new ZipFileProvider();
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        provider.Dispose();

        // After dispose, should be able to access the zip file from other code
        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        File.Exists(zipPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveToFileAsync_WithNestedEntriesDirectory_ShouldCreateNestedPath()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider { EntriesDirectory = "/configs/production" };
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var zipPath = Path.Combine(Path.GetDirectoryName(testFile.FilePath)!, "config.zip");
        using var zip = ZipFile.OpenRead(zipPath);
        var expectedEntryPath = $"configs/production/{Path.GetFileName(testFile.FilePath)}";
        var entry = zip.GetEntry(expectedEntryPath);
        entry.ShouldNotBeNull();
    }

    [Fact]
    public async Task FileExists_WithEntriesDirectory_ShouldFindEntry()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider { EntriesDirectory = "configs" };
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        var exists = provider.FileExists(testFile.FilePath);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task GetFileStream_WithEntriesDirectory_ShouldReturnStream()
    {
        using var testFile = new TemporaryFile();
        using var provider = new ZipFileProvider { EntriesDirectory = "configs" };
        var content = Encoding.UTF8.GetBytes("Test content");

        await provider.SaveToFileAsync(testFile.FilePath, content);

        using var stream = provider.GetFileStream(testFile.FilePath);
        stream.ShouldNotBeNull();

        using var reader = new StreamReader(stream);
        var readContent = await reader.ReadToEndAsync();
        readContent.ShouldBe("Test content");
    }
}
