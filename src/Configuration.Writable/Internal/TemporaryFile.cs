using System;
using System.IO;

namespace Configuration.Writable.Internal;

/// <summary>
/// Provides a mechanism for managing a temporary file that is automatically deleted when disposed. <br/>
/// If created with a directory, the directory will also be deleted upon disposal.
/// </summary>
internal sealed class TemporaryFile : IDisposable
{
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath) ?? string.Empty;
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public bool WithDirectory { get; init; } = false;

    public TemporaryFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "File path cannot be null or whitespace.",
                nameof(filePath)
            );
        }
        FilePath = filePath;
        WithDirectory = false;
    }

    public TemporaryFile()
        : this(Guid.NewGuid().ToString("N"), Path.GetRandomFileName()) { }

    public TemporaryFile(string directory, string fileName)
        : this(Path.GetTempPath(), directory, fileName) { }

    public TemporaryFile(string rootDirectory, string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException(
                "Root directory cannot be null or whitespace.",
                nameof(rootDirectory)
            );
        }
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "Directory cannot be null or whitespace.",
                nameof(directory)
            );
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "File name cannot be null or whitespace.",
                nameof(fileName)
            );
        }
        FilePath = Path.Combine(rootDirectory, directory, fileName);
        WithDirectory = true;
    }

    ~TemporaryFile() => Dispose();

    public Stream CreateTemporaryFileStream()
    {
        var dirName = DirectoryPath;
        if (WithDirectory && dirName != "" && !Directory.Exists(dirName))
        {
            Directory.CreateDirectory(dirName);
        }
        return new FileStream(
            FilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose
        );
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
            var dirName = DirectoryPath;
            if (WithDirectory && dirName != "" && Directory.Exists(dirName))
            {
                Directory.Delete(dirName, true);
            }
            GC.SuppressFinalize(this);
        }
        catch
        {
            // Ignore exceptions on dispose
        }
    }
}
