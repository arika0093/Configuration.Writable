using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Configuration.Writable.FileProvider;

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

    public SaveLocationManager() { }

    public SaveLocationManager(SaveLocationManager source)
    {
        LocationBuilders = source
            .LocationBuilders.Select(builder =>
                (ILocationBuilder)new LocationBuilderInternal((LocationBuilderInternal)builder)
            )
            .ToList();
    }

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
    /// Builds the write location by evaluating the added location providers in order.
    /// </summary>
    /// <param name="formatProvider">The format provider to determine the file extension.</param>
    /// <param name="instanceName">The instance name for default location.</param>
    /// <param name="fileProvider">The file provider to check file and directory access.</param>
    /// <param name="promoteSaveLocationEnabled">
    /// Whether an existing file should be ignored when selecting the preferred write location.
    /// </param>
    /// <returns>The first valid save location path found, or null if none are available.</returns>
    public string Build(
        FormatProvider.IWritableFormatProvider formatProvider,
        IWritableFileProvider fileProvider,
        string instanceName,
        bool promoteSaveLocationEnabled
    )
    {
        var targetPaths = GetTargetPaths(instanceName);
        // Decide the write destination based on the following priorities
        // 1. Explicit priority (descending)
        // 2. When promotion is disabled, target file already exists and able to open with write access
        // 3. Target directory can be written to (or created if it doesn't exist)
        // 4. Registration order (ascending)
        var targetPath = targetPaths
            .Select(
                (p, i) =>
                    new
                    {
                        Path = GetPathWithExtension(p.Path, formatProvider),
                        p.Priority,
                        Index = i,
                        CanWriteFile = !promoteSaveLocationEnabled
                            && fileProvider.CanWriteToFile(
                                GetPathWithExtension(p.Path, formatProvider)
                            ),
                        // A directory is considered writable if:
                        // 1. It exists and is writable (verified by CanWriteToDirectory), OR
                        // 2. The path is relative to the current directory (no explicit directory part)
                        //    and the current directory itself is writable (use full path to avoid
                        //    provider inferring directory from a filename-only argument)
                        CanWriteDir = fileProvider.CanWriteToDirectory(
                            GetPathWithExtension(p.Path, formatProvider)
                        )
                            || (
                                string.IsNullOrEmpty(Path.GetDirectoryName(p.Path))
                                && fileProvider.CanWriteToDirectory(Path.GetFullPath("."))
                            ),
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

        // Ensure the directory for the selected path exists (create if necessary)
        // and verify write access
        if (!fileProvider.EnsureDirectoryExists(targetPath.Path))
        {
            throw new InvalidOperationException(
                $"Cannot create or write to the directory for the configured save location: {targetPath.Path}"
            );
        }

        var resultPath = targetPath.Path;

        if (formatProvider is FormatProvider.FallbackFormatProvider fallbackProvider)
        {
            fallbackProvider.ValidateConfigurationPath(resultPath, fileProvider);
        }

        return resultPath;
    }

    /// <summary>
    /// Gets the highest-priority existing location suitable for reading.
    /// </summary>
    public string? BuildReadPath(
        FormatProvider.IWritableFormatProvider formatProvider,
        IWritableFileProvider fileProvider,
        string instanceName,
        bool promoteSaveLocationEnabled
    )
    {
        var targetPath = GetTargetPaths(instanceName)
            .Select(
                (path, index) =>
                    new
                    {
                        Path = GetPathWithExtension(path.Path, formatProvider),
                        path.Priority,
                        Index = index,
                        CanWriteFile = !promoteSaveLocationEnabled
                            && fileProvider.CanWriteToFile(
                                GetPathWithExtension(path.Path, formatProvider)
                            ),
                        CanWriteDir = fileProvider.CanWriteToDirectory(
                            GetPathWithExtension(path.Path, formatProvider)
                        )
                            || (
                                string.IsNullOrEmpty(Path.GetDirectoryName(path.Path))
                                && fileProvider.CanWriteToDirectory(Path.GetFullPath("."))
                            ),
                    }
            )
            .Where(path => fileProvider.FileExists(path.Path))
            .OrderByDescending(path => path.Priority)
            .ThenByDescending(path => path.CanWriteFile)
            .ThenByDescending(path => path.CanWriteDir)
            .ThenBy(path => path.Index)
            .FirstOrDefault();

        return targetPath?.Path;
    }

    private IEnumerable<LocationPathInfo> GetTargetPaths(string instanceName)
    {
        // if nothing configured, use default location
        if (LocationBuilders.Count == 0)
        {
            var filePathInDefault = GetDefaultLocationPath(instanceName);
            var lb = new LocationBuilderInternal();
            lb.UseExecutableDirectory().AddFilePath(filePathInDefault);
            LocationBuilders.Add(lb);
        }

        return LocationBuilders
            .SelectMany(builder => builder.SaveLocationPaths)
            .Where(path => !string.IsNullOrEmpty(path.Path));
    }

    private static string GetPathWithExtension(
        string path,
        FormatProvider.IWritableFormatProvider formatProvider
    )
    {
        var fileName = Path.GetFileName(path);
        return !fileName.Contains('.') && !string.IsNullOrWhiteSpace(formatProvider.FileExtension)
            ? $"{path}.{formatProvider.FileExtension}"
            : path;
    }

    /// <summary>
    /// Gets the default location path based on the instance name.
    /// </summary>
    private static string GetDefaultLocationPath(string instanceName) =>
        !string.IsNullOrWhiteSpace(instanceName) ? instanceName : DefaultFileName;
}

internal class LocationBuilderInternal : ILocationBuilder
{
    // intermediate folder before combining with file name
    private string configFolder = "";

    private readonly List<LocationPathInfo> targetPaths = [];

    public LocationBuilderInternal() { }

    public LocationBuilderInternal(LocationBuilderInternal source)
    {
        configFolder = source.configFolder;
        targetPaths.AddRange(source.targetPaths);
    }

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
