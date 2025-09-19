using System;
using System.IO;
using System.Reflection;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptions<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the name of the file used to store user settings. Defaults to "usersettings.json".
    /// </summary>
    public string FileName { get; set; } = "usersettings.json";

    /// <summary>
    /// Gets or sets the name of the folder associated with the current application.
    /// Defaults to the entry assembly name.
    /// </summary>
    public string? FolderName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name;

    /// <summary>
    /// Gets or sets the root folder path for configuration files.
    /// Defaults to the user config directory for the current platform. <br/>
    /// * On Windows, this is typically "%LOCALAPPDATA%" <br/>
    /// * On macOS, this is typically "~/Library/Application Support" <br/>
    /// * On Linux, this is typically "$XDG_CONFIG_HOME" or "~/.config"
    /// </summary>
    public string ConfigRootFolder { get; set; } =
        UserConfigurationPath.GetUserConfigRootDirectory();

    /// <summary>
    /// Gets or sets the name of the configuration instance. Defaults to Options.DefaultName ("").
    /// </summary>
    public string InstanceName { get; set; } = Microsoft.Extensions.Options.Options.DefaultName;

    /// <summary>
    /// Gets or sets the name of the configuration section. Defaults to "UserSettings".
    /// If empty, that means the root of the configuration file.
    /// </summary>
    public string SectionName { get; set; } = "UserSettings";

    /// <summary>
    /// Gets or sets a factory function to create an instance of <see cref="IWritableOptions{T}"/>.
    /// </summary>
    public Func<IServiceProvider, IWritableOptions<T>>? Provider { get; set; } = null;

    /// <summary>
    /// Gets the full file path to the configuration file, combining the root path, folder name, and file name.
    /// </summary>
    public string ConfigFilePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ConfigRootFolder))
            {
                throw new InvalidOperationException($"ConfigRootFolder cannot be null or empty.");
            }
            if (string.IsNullOrWhiteSpace(FolderName))
            {
                throw new InvalidOperationException($"FolderName cannot be null or empty.");
            }
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new InvalidOperationException($"FileName cannot be null or empty.");
            }
            var combined = Path.Combine(ConfigRootFolder, FolderName, FileName);
            return Path.GetFullPath(combined);
        }
    }
}
