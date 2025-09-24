#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.IO;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptionsBuilder<T>
    where T : class
{
    private const string DefaultFileName = "usersettings";

    /// <summary>
    /// Gets or sets a instance of <see cref="IWritableConfigProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="WritableConfigJsonProvider"/> which uses JSON format. <br/>
    /// </summary>
    public IWritableConfigProvider Provider { get; set; } = new WritableConfigJsonProvider();

    /// <summary>
    /// Gets or sets a instance of <see cref="IFileWriter"/> used to handle the file writing operations override from provider's default.
    /// </summary>
    public IFileWriter? FileWriter { get; set; } = null;

    /// <summary>
    /// Gets or sets the stream used to read the file content override from provider's default.
    /// </summary>
    public Stream? FileReadStream { get; set; } = null;

    /// <summary>
    /// Gets or sets the path of the file used to store user settings. Defaults to InstanceName or "usersettings" if InstanceName is not set. <br/>
    /// Extension is determined by the Provider. <br/>
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the name of the configuration instance. Defaults to Options.DefaultName ("").
    /// </summary>
    public string InstanceName { get; set; } = Microsoft.Extensions.Options.Options.DefaultName;

    /// <summary>
    /// Gets or sets the name of the configuration section. Defaults to "UserSettings".
    /// If empty, that means the root of the configuration file.
    /// If use multiple configuration file for same type T, you must set different SectionName for each.
    /// </summary>
    public string SectionRootName
    {
        get => _sectionRootName;
        set
        {
            if (value == null)
            {
                _sectionRootName = string.Empty;
                return;
            }
            if (value.Contains(":") || value.Contains("__"))
            {
                throw new ArgumentException(
                    "SectionName cannot contain ':' or '__' characters. These are reserved for hierarchical section delimiters."
                );
            }
            _sectionRootName = value.Trim();
        }
    }
    private string _sectionRootName = "UserSettings";

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
            var fileName = FilePath;
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
            // if no extension, add default extension
            var fileNameWithExtension = Path.GetFileName(fileName);
            if (
                !fileNameWithExtension.Contains(".")
                && !string.IsNullOrWhiteSpace(Provider.FileExtension)
            )
            {
                fileNameWithExtension += $".{Provider.FileExtension}";
            }
            // if ConfigFolder is set, combine it with the directory of FileName (if any)
            var directoryName = Path.GetDirectoryName(fileName) ?? "";
            var combinedDir = string.IsNullOrWhiteSpace(ConfigFolder)
                ? Path.Combine(directoryName, fileNameWithExtension)
                : Path.Combine(ConfigFolder, directoryName, fileNameWithExtension);
            return Path.GetFullPath(combinedDir);
        }
    }

    /// <summary>
    /// Gets the full section name composed of the section root name and instance name.
    /// </summary>
    /// <remarks>
    /// multiple configuration files for the same type T must have different SectionName values.
    /// so combine SectionRootName and InstanceName to make it unique.
    /// </remarks>
    public string SectionName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SectionRootName))
            {
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(InstanceName))
            {
                return SectionRootName;
            }
            return $"{SectionRootName}-{InstanceName}";
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
    /// <returns>The full path to the configuration file.</returns>
    public string UseStandardSaveLocation(string applicationId)
    {
        var root = UserConfigurationPath.GetUserConfigRootDirectory();
        ConfigFolder = Path.Combine(root, applicationId);
        return ConfigFilePath;
    }

    /// <summary>
    /// Configures the current instance to use the specified in-memory file writer for file operations. for testing purpose.
    /// </summary>
    /// <param name="inMemoryFileWriter">The in-memory file writer to use for subsequent file write and read operations.</param>
    public void UseInMemoryFileWriter(InMemoryFileWriter inMemoryFileWriter)
    {
        FileWriter = inMemoryFileWriter;
        FileReadStream = inMemoryFileWriter.GetFileStream(ConfigFilePath);
    }

    /// <summary>
    /// Creates a new instance of writable configuration options for the specified type.
    /// </summary>
    public WritableConfigurationOptions<T> BuildOptions()
    {
        return new WritableConfigurationOptions<T>
        {
            Provider = Provider,
            ConfigFilePath = ConfigFilePath,
            InstanceName = InstanceName,
            SectionName = SectionRootName,
        };
    }

    // configuration folder path, if set, appended to the directory of FileName (if any)
    private string? ConfigFolder { get; set; } = null;
}
