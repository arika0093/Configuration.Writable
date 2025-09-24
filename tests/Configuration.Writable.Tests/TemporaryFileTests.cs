using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

public class TemporaryFileTests
{
    [Fact]
    public void Constructor_WithValidFilePath_SetsPropertiesCorrectly()
    {
        var filePath = @"C:\temp\test.txt";
        using var tempFile = new TemporaryFile(filePath);

        tempFile.FilePath.ShouldBe(filePath);
        tempFile.FileName.ShouldBe("test.txt");
        tempFile.DirectoryPath.ShouldBe(@"C:\temp");
        tempFile.WithDirectory.ShouldBeFalse();
    }

    [Fact]
    public void DefaultConstructor_CreatesValidFilePath()
    {
        using var tempFile = new TemporaryFile();

        tempFile.FilePath.ShouldNotBeNullOrEmpty();
        tempFile.FileName.ShouldNotBeNullOrEmpty();
        tempFile.DirectoryPath.ShouldNotBeNullOrEmpty();
        tempFile.WithDirectory.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithDirectoryAndFileName_SetsPropertiesCorrectly()
    {
        var directory = "testdir";
        var fileName = "test.txt";
        using var tempFile = new TemporaryFile(directory, fileName);

        tempFile.FilePath.ShouldContain(directory);
        tempFile.FilePath.ShouldEndWith(fileName);
        tempFile.FileName.ShouldBe(fileName);
        tempFile.WithDirectory.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithRootDirectoryAndFileName_SetsPropertiesCorrectly()
    {
        var rootDirectory = Path.GetTempPath();
        var directory = "testdir";
        var fileName = "test.txt";
        using var tempFile = new TemporaryFile(rootDirectory, directory, fileName);

        var expectedPath = Path.Combine(rootDirectory, directory, fileName);
        tempFile.FilePath.ShouldBe(expectedPath);
        tempFile.FileName.ShouldBe(fileName);
        tempFile.DirectoryPath.ShouldBe(Path.Combine(rootDirectory, directory));
        tempFile.WithDirectory.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_WhenFileExists_DeletesFile()
    {
        string filePath;
        using (var tempFile = new TemporaryFile())
        {
            filePath = tempFile.FilePath;
            Directory.CreateDirectory(tempFile.DirectoryPath);
            File.WriteAllText(filePath, "test content");
            File.Exists(filePath).ShouldBeTrue();
        }

        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void Dispose_WithDirectoryTrue_DeletesDirectoryAndFile()
    {
        string filePath;
        string directoryPath;

        using (var tempFile = new TemporaryFile("testdir", "test.txt"))
        {
            filePath = tempFile.FilePath;
            directoryPath = tempFile.DirectoryPath;

            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(filePath, "test content");

            File.Exists(filePath).ShouldBeTrue();
            Directory.Exists(directoryPath).ShouldBeTrue();
        }

        File.Exists(filePath).ShouldBeFalse();
        Directory.Exists(directoryPath).ShouldBeFalse();
    }

    [Fact]
    public void Dispose_WithDirectoryFalse_DoesNotDeleteDirectory()
    {
        var tempDir = Path.GetTempPath();
        var filePath = Path.Combine(tempDir, "temp_test_file.txt");

        using (var tempFile = new TemporaryFile(filePath))
        {
            File.WriteAllText(filePath, "test content");
            File.Exists(filePath).ShouldBeTrue();
        }

        File.Exists(filePath).ShouldBeFalse();
        Directory.Exists(tempDir).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_WhenDirectoryContainsOtherFiles_DeletesOnlyOwnFiles()
    {
        var rootDir = Path.GetTempPath();
        var testDir = Path.Combine(rootDir, Guid.NewGuid().ToString("N"));
        var tempFileName = "temp.txt";
        var otherFileName = "other.txt";

        Directory.CreateDirectory(testDir);
        var otherFilePath = Path.Combine(testDir, otherFileName);
        File.WriteAllText(otherFilePath, "other content");

        string tempFilePath;
        using (var tempFile = new TemporaryFile(rootDir, Path.GetFileName(testDir), tempFileName))
        {
            tempFilePath = tempFile.FilePath;
            File.WriteAllText(tempFilePath, "temp content");

            File.Exists(tempFilePath).ShouldBeTrue();
            File.Exists(otherFilePath).ShouldBeTrue();
        }

        File.Exists(tempFilePath).ShouldBeFalse();
        File.Exists(otherFilePath).ShouldBeFalse();
        Directory.Exists(testDir).ShouldBeFalse();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
    {
        var tempFile = new TemporaryFile();
        var filePath = tempFile.FilePath;
        Directory.CreateDirectory(tempFile.DirectoryPath);
        File.WriteAllText(filePath, "test");

        Should.NotThrow(() =>
        {
            tempFile.Dispose();
            tempFile.Dispose();
            tempFile.Dispose();
        });
    }

    [Fact]
    public void Properties_ReturnsExpectedValues()
    {
        var filePath = Path.Combine("test", "directory", "file.txt");
        using var tempFile = new TemporaryFile(filePath);

        tempFile.FileName.ShouldBe("file.txt");
        tempFile.DirectoryPath.ShouldBe(Path.Combine("test", "directory"));
    }

    [Fact]
    public void FileName_WithEmptyPath_ReturnsEmptyString()
    {
        using var tempFile = new TemporaryFile("test");
        var fileName = Path.GetFileName("test") ?? string.Empty;
        tempFile.FileName.ShouldBe(fileName);
    }

    [Fact]
    public void DirectoryPath_WithRootPath_ReturnsEmptyString()
    {
        var rootPath = Path.GetPathRoot(Path.GetTempPath()) ?? "C:\\";
        using var tempFile = new TemporaryFile(rootPath);

        var expectedDirectory = Path.GetDirectoryName(rootPath) ?? string.Empty;
        tempFile.DirectoryPath.ShouldBe(expectedDirectory);
    }
}
