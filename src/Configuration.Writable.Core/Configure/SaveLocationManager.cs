using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Configuration.Writable.Configure;

/// <summary>
/// Manages multiple location builders to determine the save location for application settings.
/// </summary>
internal class SaveLocationManager
{
    private const string DefaultFileName = "usersettings";

    /// <summary>
    /// Gets the list of location builders.
    /// </summary>
    public List<ILocationBuilder> LocationBuilders { get; private set; } = [];

    /// <summary>
    /// Creates a new location builder and adds it to the manager.
    /// </summary>
    public ILocationBuilderWithDirectory MakeLocationBuilder()
    {
        var builder = new LocationBuilderInternal();
        LocationBuilders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Builds the save location by evaluating the added location providers in order.
    /// </summary>
    /// <returns>The first valid save location path found, or null if none are available.</returns>
    public string Build(IFormatProvider formatProvider)
    {
        string resultPath = "";
        // if nothing configured, use default "usersettings" in executable directory
        if (LocationBuilders.Count == 0)
        {
            var lb = new LocationBuilderInternal();
            lb.UseExecutableDirectory().AddFilePath(DefaultFileName);
            LocationBuilders.Add(lb);
        }

        // Decide the write destination based on the following priorities
        // 1. Writable directory
        // 2. Target file already exists
        // 3. Target directory already exists
        // 4. Registration order (ascending)
        var targetPath =
            LocationBuilders
                .SelectMany(p => p.SaveLocationPaths)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(
                    (p, i) =>
                        new
                        {
                            Path = p,
                            Index = i,
                            ExistDirectory = Directory.Exists(Path.GetDirectoryName(p) ?? ""),
                            ExistFile = File.Exists(p),
                            CanWrite = CanWriteToDirectory(p),
                        }
                )
                .OrderByDescending(p => p.CanWrite)
                .ThenByDescending(p => p.ExistFile)
                .ThenByDescending(p => p.ExistDirectory)
                .ThenBy(p => p.Index)
                .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No valid save location could be determined from the configured location providers."
            );

        // if no file extension, add from format provider
        var fileName = Path.GetFileName(targetPath.Path);
        if (!fileName.Contains('.') && !string.IsNullOrWhiteSpace(formatProvider.FileExtension))
        {
            resultPath = $"{targetPath.Path}.{formatProvider.FileExtension}";
        }
        else
        {
            resultPath = targetPath.Path;
        }
        return resultPath;
    }

    /// <summary>
    /// Checks if the application can write to the specified directory.
    /// </summary>
    private static bool CanWriteToDirectory(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path) ?? "";
            var testFilePath = Path.Combine(directory, Path.GetRandomFileName());
            // create and delete a temporary file to test write access
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
}

internal class LocationBuilderInternal : ILocationBuilderWithDirectory
{
    // intermediate folder before combining with file name
    private string configFolder = "";

    private readonly List<string> targetPaths = [];

    /// <inheritdoc />
    public IEnumerable<string> SaveLocationPaths => targetPaths;

    /// <inheritdoc />
    public ILocationBuilder AddFilePath(string path)
    {
        var combined = Path.Combine(configFolder, path);
        var absolutePath = Path.GetFullPath(combined);
        targetPaths.Add(absolutePath);
        return this;
    }

    /// <inheritdoc />
    public ILocationBuilder UseStandardSaveDirectory(string applicationId, bool enabled = true)
    {
        if (enabled)
        {
            var root = StandardSaveLocationUtility.GetConfigDirectory();
            configFolder = Path.Combine(root, applicationId);
        }
        return this;
    }

    /// <inheritdoc />
    public ILocationBuilder UseCurrentDirectory(bool enabled = true)
    {
        if (enabled)
        {
            configFolder = Directory.GetCurrentDirectory();
        }
        return this;
    }

    /// <inheritdoc />
    public ILocationBuilder UseExecutableDirectory(bool enabled = true)
    {
        if (enabled)
        {
            configFolder = AppContext.BaseDirectory;
        }
        return this;
    }

    /// <inheritdoc />
    public ILocationBuilder UseSpecialFolder(Environment.SpecialFolder folder, bool enabled = true)
    {
        if (enabled)
        {
            configFolder = Environment.GetFolderPath(folder);
        }
        return this;
    }
}
