using System;
using System.IO;
using System.Reflection;
using Configuration.Writable.Internal;
using Configuration.Writable.Provider;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptions<T>
    where T : class
{
    /// <summary>
    /// Gets or sets a instance of <see cref="IWritableConfigProvider{T}"/> used to handle the serialization and deserialization of the configuration data.
    /// </summary>
    public IWritableConfigProvider<T> Provider { get; set; } = new WritableConfigJsonProvider<T>();

    /// <summary>
    /// Gets or sets the name of the file used to store user settings. Defaults to "usersettings.json".
    /// </summary>
    public string FileName { get; set; } = "usersettings.json";

    /// <summary>
    /// eng: Gets or sets the folder name where the settings are saved. <br/>
    /// If NULL, FileName is treated as a relative path.
    /// If a value is provided, the file is saved under the specified folder with the name of FileName. <br/>
    /// </summary>
    public string? ConfigFolder { get; set; } = null;

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
    /// Indicates whether to automatically register <typeparamref name="T"/> in the DI container. Defaults to false. <br/>
    /// Enabling this allows you to obtain the instance directly from the DI container,
    /// which is convenient, but automatic value updates are not provided, so be careful with the lifecycle. <br/>
    /// if you specify InstanceName, you can get it with [FromKeyedServices("instance-name")].
    /// </summary>
    public bool RegisterInstanceToContainer { get; set; } = false;

    /// <summary>
    /// Gets the full file path to the configuration file, combining config folder and file name.
    /// </summary>
    public string ConfigFilePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new InvalidOperationException($"FileName cannot be null or empty.");
            }
            if (string.IsNullOrWhiteSpace(ConfigFolder))
            {
                return Path.GetFullPath(FileName);
            }
            var combined = Path.Combine(ConfigFolder, FileName);
            return Path.GetFullPath(combined);
        }
    }

    /// <summary>
    /// Sets the configuration folder to the standard save location for the specified application.
    /// </summary>
    /// <remarks>
    /// in Windows: %APPDATA%/<paramref name="applicationId"/> <br/>
    /// in macOS: ~/Library/Application Support/<paramref name="applicationId"/> <br/>
    /// in Linux: $XDG_CONFIG_HOME/<paramref name="applicationId"/>
    /// </remarks>
    /// <param name="applicationId">The unique identifier of the application. This is used to determine the subdirectory within the user
    /// configuration root directory.</param>
    public void UseStandardSaveLocation(string applicationId)
    {
        var root = UserConfigurationPath.GetUserConfigRootDirectory();
        ConfigFolder = Path.Combine(root, applicationId);
    }
}
