#pragma warning disable S2326 // Unused type parameters should be removed
using System.IO;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptions<T>
    where T : class
{
    private const string DefaultFileName = "usersettings";

    /// <summary>
    /// Gets or sets a instance of <see cref="IWritableConfigProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="WritableConfigJsonProvider"/> which uses JSON format. <br/>
    /// </summary>
    public IWritableConfigProvider Provider { get; set; } = new WritableConfigJsonProvider();

    /// <summary>
    /// Gets or sets a instance of <see cref="IFileWriter"/> used to handle the file writing operations.
    /// </summary>
    public IFileWriter FileWriter { get; set; } = new CommonFileWriter();

    /// <summary>
    /// Gets or sets the name of the file used to store user settings. Defaults to InstanceName or "usersettings" if InstanceName is not set. <br/>
    /// Extension is determined by the Provider. <br/>
    /// </summary>
    public string? FileName { get; set; }

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
    /// If use multiple configuration file for same type T, you must set different SectionName for each.
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
            var fileName = FileName;
            if (fileName == null)
            {
                if (InstanceName != Microsoft.Extensions.Options.Options.DefaultName)
                {
                    fileName = InstanceName;
                }
                else
                {
                    fileName = DefaultFileName;
                }
            }
            var fileNameWithExtension = fileName;
            if (!fileNameWithExtension.Contains("."))
            {
                fileNameWithExtension += $".{Provider.FileExtension}";
            }
            if (string.IsNullOrWhiteSpace(ConfigFolder))
            {
                return Path.GetFullPath(fileNameWithExtension);
            }
            var combined = Path.Combine(ConfigFolder, fileNameWithExtension);
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
