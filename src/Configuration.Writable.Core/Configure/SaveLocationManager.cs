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
    /// Gets the first registered save location path based on priority.
    /// </summary>
    public string? LocationPath =>
        LocationBuilders
            .SelectMany(b => b.SaveLocationPaths)
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault()
            ?.Path;

    /// <summary>
    /// Creates a new location builder and adds it to the manager.
    /// </summary>
    public LocationBuilderInternal MakeLocationBuilder()
    {
        var builder = new LocationBuilderInternal();
        LocationBuilders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Builds the save location by evaluating the added location providers in order.
    /// </summary>
    /// <returns>The first valid save location path found, or null if none are available.</returns>
    public string Build(IFormatProvider formatProvider, string instanceName)
    {
        string resultPath = "";
        // if nothing configured, use default location
        if (LocationBuilders.Count == 0)
        {
            var filePathInDefault = GetDefaultLocationPath(instanceName);
            var lb = new LocationBuilderInternal();
            lb.UseExecutableDirectory().AddFilePath(filePathInDefault);
            LocationBuilders.Add(lb);
        }

        // Decide the write destination based on the following priorities
        // 1. Explicit priority (descending)
        // 2. Target file already exists and able to open with write access
        // 3. Target directory already exists and able to create file
        // 4. Registration order (ascending)
        var targetPath = LocationBuilders
            .SelectMany(p => p.SaveLocationPaths)
            .Where(p => !string.IsNullOrEmpty(p.Path))
            .Select(
                (p, i) =>
                    new
                    {
                        p.Path,
                        p.Priority,
                        Index = i,
                        CanWriteFile = CheckCanOpenFileWithWriteAccess(p.Path),
                        CanWriteDir = CheckIsWritableToDirectory(p.Path),
                    }
            )
            .OrderByDescending(p => p.Priority)
            .ThenByDescending(p => p.CanWriteFile)
            .ThenByDescending(p => p.CanWriteDir)
            .ThenBy(p => p.Index)
            .FirstOrDefault();

        if (targetPath == null)
        {
            throw new InvalidOperationException(
                "No valid save location could be determined from the configured location providers."
            );
        }

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
    /// Gets the default location path based on the instance name.
    /// </summary>
    private static string GetDefaultLocationPath(string instanceName) =>
        !string.IsNullOrWhiteSpace(instanceName) ? instanceName : DefaultFileName;
    
    /// <summary>
    /// Checks if the application can open the specified file with write access.
    /// </summary>
    private static bool CheckCanOpenFileWithWriteAccess(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                // If the file does not exist, we cannot open it with write access
                return false;
            }

            using var stream = File.Open(path, FileMode.Open, FileAccess.Write);
            // If we can open the file with write access, return true
            return true;
        }
        catch
        {
            // If an exception occurs, we cannot write to the file
            return false;
        }
    }

    /// <summary>
    /// Checks if the application can write to the specified directory.
    /// </summary>
    private static bool CheckIsWritableToDirectory(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path) ?? "";
            if (!Directory.Exists(directory))
            {
                return false;
            }

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

internal class LocationBuilderInternal : ILocationBuilder
{
    // intermediate folder before combining with file name
    private string configFolder = "";

    private readonly List<LocationPathInfo> targetPaths = [];

    /// <inheritdoc />
    public IEnumerable<LocationPathInfo> SaveLocationPaths => targetPaths;

    /// <inheritdoc />
    public ILocationBuilder AddFilePath(string path, int priority = 0)
    {
        var combined = Path.Combine(configFolder, path);
        var absolutePath = Path.GetFullPath(combined);
        targetPaths.Add(new LocationPathInfo(absolutePath, priority));
        return this;
    }

    /// <summary>
    /// Sets the configuration folder to the standard save location for the specified application.
    /// </summary>
    public ILocationBuilder UseStandardSaveDirectory(string applicationId)
    {
        var root = StandardSaveLocationUtility.GetConfigDirectory();
        configFolder = Path.Combine(root, applicationId);
        return this;
    }

    /// <summary>
    /// Sets the configuration folder to the directory where the executable is located. (default behavior)
    /// </summary>
    public ILocationBuilder UseCurrentDirectory()
    {
        configFolder = Directory.GetCurrentDirectory();
        return this;
    }

    /// <summary>
    /// Sets the configuration folder to the current working directory.
    /// </summary>
    public ILocationBuilder UseExecutableDirectory()
    {
        configFolder = AppContext.BaseDirectory;
        return this;
    }

    /// <summary>
    /// Sets the configuration folder to a special folder defined by <see cref="Environment.SpecialFolder"/>.
    /// </summary>
    public ILocationBuilder UseSpecialFolder(Environment.SpecialFolder folder)
    {
        configFolder = Environment.GetFolderPath(folder);
        return this;
    }

    /// <summary>
    /// Sets the configuration folder to a custom folder path.
    /// </summary>
    public ILocationBuilder UseCustomDirectory(string directoryPath)
    {
        configFolder = directoryPath;
        return this;
    }
}
