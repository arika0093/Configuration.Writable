#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptionsBuilder<T>
    where T : class, new()
{
    private const string DefaultSectionName = "default";
    private const string DefaultFileName = "usersettings";
    private readonly List<Func<T, ValidateOptionsResult>> _validators = [];

    /// <summary>
    /// Gets or sets a instance of <see cref="IFormatProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="JsonFormatProvider"/> which uses JSON format. <br/>
    /// </summary>
    public IFormatProvider FormatProvider { get; set; } = new JsonFormatProvider();

    /// <summary>
    /// Gets or sets a instance of <see cref="IFileProvider"/> used to handle the file writing operations override from provider's default.
    /// </summary>
    public IFileProvider? FileProvider { get; set; } = null;

    /// <summary>
    /// Gets or sets the path of the file used to store user settings. <br/>
    /// Defaults(null) to "usersettings" or InstanceName if specified. <br/>
    /// Extension is determined by the <see cref="IFormatProvider"/> so it can be omitted.
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
    /// Gets or sets a value indicating whether validation using data annotation attributes is enabled. Defaults to true.
    /// </summary>
    public bool UseDataAnnotationsValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the logger for configuration operations. Defaults to null. <br/>
    /// If null, logging is disabled or use provider's default logger.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Get or sets the name of the configuration section. <br/>
    /// You can use ":" or "__" to specify nested sections, e.g. "Parent:Child". <br/>
    /// If empty that means the root of the configuration file. <br/>
    /// If null, the default section name will be used (empty string).
    /// </summary>
    [AllowNull]
    public string SectionName
    {
        get { return _sectionName ?? DefaultSectionName; }
        set { _sectionName = value; }
    }
    private string? _sectionName = null;

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
    /// Adds a custom validation function to be executed before saving configuration.
    /// </summary>
    /// <param name="validator">A function that validates the configuration and returns a <see cref="ValidateOptionsResult"/>.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public void WithValidatorFunction(Func<T, ValidateOptionsResult> validator)
    {
        _validators.Add(validator);
    }

    /// <summary>
    /// Adds a custom validator of type <typeparamref name="TValidator"/> to be executed before saving configuration.
    /// </summary>
    /// <typeparam name="TValidator">The type of the validator to add. Must implement <see cref="IValidateOptions{TOptions}"/> and have a parameterless constructor.</typeparam>
    public void WithValidator<TValidator>()
        where TValidator : IValidateOptions<T>, new()
    {
        var validatorInstance = new TValidator();
        WithValidator(validatorInstance);
    }

    /// <summary>
    /// Adds a custom validator to be executed before saving configuration.
    /// </summary>
    /// <param name="validator">An instance of <see cref="IValidateOptions{TOptions}"/> to validate the configuration.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public void WithValidator(IValidateOptions<T> validator)
    {
        _validators.Add(value => validator.Validate(null, value));
    }

    /// <summary>
    /// Creates a new instance of writable configuration options for the specified type.
    /// </summary>
    public WritableConfigurationOptions<T> BuildOptions()
    {
        var validator = BuildValidator();
        // override provider's file provider if set
        if (FileProvider != null)
        {
            FormatProvider.FileProvider = FileProvider;
        }

        return new WritableConfigurationOptions<T>
        {
            Provider = FormatProvider,
            ConfigFilePath = ConfigFilePath,
            InstanceName = InstanceName,
            SectionName = SectionName,
            Logger = Logger,
            Validator = validator,
        };
    }

    /// <summary>
    /// Gets the full file path to the configuration file, combining config folder and file name. <br/>
    /// If ConfigFolder is set, the file will be saved in that folder; otherwise, it will be saved in the same folder as the executable.
    /// </summary>
    internal string ConfigFilePath
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
    /// Builds the composite validator from all registered validators.
    /// </summary>
    private Func<T, ValidateOptionsResult>? BuildValidator()
    {
        var validators = new List<Func<T, ValidateOptionsResult>>(_validators);

        if (UseDataAnnotationsValidation)
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
            return CombineValidateOptionsResults(results);
        };
    }

    /// <summary>
    /// Combines multiple ValidateOptionsResult into a single result.
    /// </summary>
    private static ValidateOptionsResult CombineValidateOptionsResults(
        List<ValidateOptionsResult> results
    )
    {
        var allFailures = results.Where(r => r.Failed).SelectMany(r => r.Failures ?? []).ToList();

        return allFailures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(allFailures);
    }

    /// <summary>
    /// Validates an object using Data Annotations.
    /// </summary>
    private static ValidateOptionsResult ValidateWithDataAnnotations(T value)
    {
        var context = new ValidationContext(value);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            value,
            context,
            validationResults,
            validateAllProperties: true
        );

        if (isValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = validationResults
            .Where(r => r.ErrorMessage != null)
            .Select(r => r.ErrorMessage!)
            .ToList();

        return ValidateOptionsResult.Fail(errors);
    }

    // configuration folder path, if set, appended to the directory of FileName (if any)
    private string? ConfigFolder { get; set; } = null;

    // get the file name with extension, if no extension, add default extension from provider
    private string FilePathWithExtension
    {
        get
        {
            var filePath = FilePath;
#if NET
            if (string.IsNullOrWhiteSpace(filePath))
#else
            if (string.IsNullOrWhiteSpace(filePath) || filePath is null)
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
            if (!fileName.Contains(".") && !string.IsNullOrWhiteSpace(FormatProvider.FileExtension))
            {
                filePath += $".{FormatProvider.FileExtension}";
            }
            return filePath;
        }
    }
}
