#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;
using Configuration.Writable.Validation;
using Microsoft.Extensions.Logging;
using ValidationResult = Configuration.Writable.Validation.ValidationResult;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptionsBuilder<T>
    where T : class
{
    private const string DefaultFileName = "usersettings";
    private readonly List<Func<T, ValidationResult>> _validators = new();

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
    /// Gets or sets the path of the file used to store user settings. <br/>
    /// Defaults(null) to "usersettings" or InstanceName if specified. <br/>
    /// Extension is determined by the Provider so it can be omitted.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the name of the configuration instance. Defaults to Options.DefaultName ("").
    /// </summary>
    public string InstanceName { get; set; } = Microsoft.Extensions.Options.Options.DefaultName;

    /// <summary>
    /// Indicates whether to automatically register <typeparamref name="T"/> in the DI container. Defaults to false. <br/>
    /// Enabling this allows you to obtain the instance directly from the DI container,
    /// which is convenient, but automatic value updates are not provided, so be careful with the lifecycle. <br/>
    /// if you specify InstanceName, you can get it with [FromKeyedServices("instance-name")].
    /// </summary>
    public bool RegisterInstanceToContainer { get; set; } = false;

    /// <summary>
    /// Gets or sets the logger for configuration operations.
    /// If null, logging is disabled or use provider's default logger. Defaults to null.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Indicates whether to validate using Data Annotations. Defaults to false.
    /// </summary>
    private bool UseDataAnnotations { get; set; } = false;

    /// <summary>
    /// Gets the full file path to the configuration file, combining config folder and file name. <br/>
    /// If ConfigFolder is set, the file will be saved in that folder; otherwise, it will be saved in the same folder as the executable.
    /// </summary>
    public string ConfigFilePath
    {
        get
        {
            var filePath = FilePathWithExtension;
            // if ConfigFolder is not set, use executable directory as default
            if (string.IsNullOrWhiteSpace(ConfigFolder))
            {
                UseExecutableDirectory();
            }
            // ConfigFolder is not null
            var combinedDir = Path.Combine(ConfigFolder!, filePath);
            var fullPath = Path.GetFullPath(combinedDir);
            return fullPath;
        }
    }

    /// <summary>
    /// Get or sets the name of the configuration section. <br/>
    /// You can use ":" or "__" to specify nested sections, e.g. "Parent:Child". <br/>
    /// If empty that means the root of the configuration file. <br/>
    /// If null, the default section name will be used.
    /// </summary>
    [AllowNull]
    public string SectionName
    {
        get { return _sectionName ?? DefaultSectionName; }
        set { _sectionName = value; }
    }
    private string? _sectionName = null;

    /// <summary>
    /// Gets or sets the default root element name used for configuration sections.
    /// Defaults to "UserSettings".
    /// </summary>
    public string DefaultSectionRootName { get; set; } = "UserSettings";

    /// <summary>
    /// Gets the default configuration section name. Defaults to "UserSettings:{TypeName}[-{InstanceName}]".
    /// </summary>
    /// <remarks>
    /// If you want override the section name, set <see cref="SectionName"/> property. <br/>
    /// Or you want override only the root part, set <see cref="DefaultSectionRootName"/> property.
    /// </remarks>
    public string DefaultSectionName
    {
        get
        {
            var section = $"{DefaultSectionRootName}:{typeof(T).Name}";
            if (!string.IsNullOrWhiteSpace(InstanceName))
            {
                section += $"-{InstanceName}";
            }
            return section;
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
    /// Sets the configuration folder to the directory where the executable is located. (default behavior)
    /// </summary>
    /// <remarks>
    /// This uses <see cref="AppContext.BaseDirectory"/> to determine the executable directory.
    /// </remarks>
    /// <returns>The full path to the configuration file.</returns>
    public string UseExecutableDirectory()
    {
        ConfigFolder = AppContext.BaseDirectory;
        return ConfigFilePath;
    }

    /// <summary>
    /// Sets the configuration folder to the current working directory.
    /// </summary>
    /// <remarks>
    /// This uses <see cref="Directory.GetCurrentDirectory()"/> to determine the current directory.
    /// </remarks>
    /// <returns>The full path to the configuration file.</returns>
    public string UseCurrentDirectory()
    {
        ConfigFolder = Directory.GetCurrentDirectory();
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
    /// Adds a custom validation function to be executed before saving configuration.
    /// </summary>
    /// <param name="validator">A function that validates the configuration and returns a <see cref="ValidationResult"/>.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WritableConfigurationOptionsBuilder<T> WithValidation(
        Func<T, ValidationResult> validator
    )
    {
        if (validator == null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        _validators.Add(validator);
        return this;
    }

    /// <summary>
    /// Adds a custom validator to be executed before saving configuration.
    /// </summary>
    /// <param name="validator">An instance of <see cref="IValidator{T}"/> to validate the configuration.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public WritableConfigurationOptionsBuilder<T> WithValidator(IValidator<T> validator)
    {
        if (validator == null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        _validators.Add(validator.Validate);
        return this;
    }

    /// <summary>
    /// Enables validation using Data Annotations attributes on the configuration class.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    public WritableConfigurationOptionsBuilder<T> ValidateDataAnnotations()
    {
        UseDataAnnotations = true;
        return this;
    }

    /// <summary>
    /// Creates a new instance of writable configuration options for the specified type.
    /// </summary>
    public WritableConfigurationOptions<T> BuildOptions()
    {
        var validator = BuildValidator();

        return new WritableConfigurationOptions<T>
        {
            Provider = Provider,
            ConfigFilePath = ConfigFilePath,
            InstanceName = InstanceName,
            SectionName = SectionName,
            Logger = Logger,
            Validator = validator,
        };
    }

    /// <summary>
    /// Builds the composite validator from all registered validators.
    /// </summary>
    private Func<T, ValidationResult>? BuildValidator()
    {
        var validators = new List<Func<T, ValidationResult>>(_validators);

        // Add Data Annotations validator if enabled
        if (UseDataAnnotations)
        {
            validators.Add(ValidateWithDataAnnotations);
        }

        if (validators.Count == 0)
        {
            return null;
        }

        return value =>
        {
            var results = validators.Select(v => v(value)).ToList();
            return ValidationResult.Combine(results.ToArray());
        };
    }

    /// <summary>
    /// Validates an object using Data Annotations.
    /// </summary>
    private static ValidationResult ValidateWithDataAnnotations(T value)
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(value);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var isValid = Validator.TryValidateObject(
            value,
            context,
            validationResults,
            validateAllProperties: true
        );

        if (isValid)
        {
            return ValidationResult.Success();
        }

        var errors = validationResults
            .Where(r => r.ErrorMessage != null)
            .Select(r => r.ErrorMessage!)
            .ToList();

        return ValidationResult.Failure(errors);
    }

    // configuration folder path, if set, appended to the directory of FileName (if any)
    private string? ConfigFolder { get; set; } = null;

    // get the file name with extension, if no extension, add default extension from provider
    private string FilePathWithExtension
    {
        get
        {
            var filePath = FilePath;
#if NETSTANDARD
            if (string.IsNullOrWhiteSpace(filePath) || filePath is null)
#else
            if (string.IsNullOrWhiteSpace(filePath))
#endif
            {
                if (InstanceName != Microsoft.Extensions.Options.Options.DefaultName)
                {
                    filePath = InstanceName;
                }
                else
                {
                    filePath = DefaultFileName;
                }
            }
            // if no extension, add default extension
            var fileName = Path.GetFileName(filePath);
            if (!fileName.Contains(".") && !string.IsNullOrWhiteSpace(Provider.FileExtension))
            {
                filePath += $".{Provider.FileExtension}";
            }
            return filePath;
        }
    }
}
