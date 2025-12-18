#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Configure;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableOptionsConfigBuilder<T> : ILocationBuilderWithDirectory
    where T : class, new()
{
    private const string DefaultSectionName = "";
    private readonly List<Func<T, ValidateOptionsResult>> _validators = [];
    private readonly SaveLocationManager _saveLocationManager = new();

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
    public string? FilePath
    {
        get => _saveLocationManager.Build(FormatProvider);
        set
        {
            _saveLocationManager.LocationBuilders.Clear();
            if (value != null)
            {
                AddFilePath(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the name of the configuration instance. Defaults to Options.DefaultName ("").
    /// </summary>
    public string InstanceName { get; set; } = Microsoft.Extensions.Options.Options.DefaultName;

    /// <summary>
    /// Gets or sets the throttle duration in milliseconds for change events.
    /// This helps to prevent excessive event firing during rapid changes. <br/>
    /// Defaults to 1000 ms.
    /// </summary>
    public int OnChangeThrottleMs { get; set; } = 1000;

    /// <summary>
    /// Indicates whether to automatically register <typeparamref name="T"/> in the DI container. Defaults to false. <br/>
    /// Enabling this allows you to obtain the instance directly from the DI container,
    /// which is convenient, but automatic value updates are not provided, so be careful with the lifecycle. <br/>
    /// if you specify InstanceName, you can get it with [FromKeyedServices("instance-name")].
    /// </summary>
    public bool RegisterInstanceToContainer { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether validation using data annotation attributes is enabled. Defaults to true. <br/>
    /// If you want to use Source-Generator based validation or custom validation only, set this to false.
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
    /// If empty that means the root of the configuration file.
    /// </summary>
    public string SectionName { get; set; } = DefaultSectionName;

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
    public WritableOptionsConfiguration<T> BuildOptions()
    {
        var configFilePath = _saveLocationManager.Build(FormatProvider);
        var validator = BuildValidator();
        // override provider's file provider if set
        if (FileProvider != null)
        {
            FormatProvider.FileProvider = FileProvider;
        }

        return new WritableOptionsConfiguration<T>
        {
            FormatProvider = FormatProvider,
            ConfigFilePath = configFilePath,
            InstanceName = InstanceName,
            SectionName = SectionName,
            OnChangeThrottleMs = OnChangeThrottleMs,
            Logger = Logger,
            Validator = validator,
        };
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

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerable<string> SaveLocationPaths =>
        _saveLocationManager.LocationBuilders.SelectMany(b => b.SaveLocationPaths);

    /// <inheritdoc />
    public ILocationBuilder UseStandardSaveDirectory(string applicationId, bool enabled = true) =>
        _saveLocationManager.MakeLocationBuilder().UseStandardSaveDirectory(applicationId, enabled);

    /// <inheritdoc />
    public ILocationBuilder UseExecutableDirectory(bool enabled = true) =>
        _saveLocationManager.MakeLocationBuilder().UseExecutableDirectory(enabled);

    /// <inheritdoc />
    public ILocationBuilder UseCurrentDirectory(bool enabled = true) =>
        _saveLocationManager.MakeLocationBuilder().UseCurrentDirectory(enabled);

    /// <inheritdoc />
    public ILocationBuilder UseSpecialFolder(
        Environment.SpecialFolder folder,
        bool enabled = true
    ) => _saveLocationManager.MakeLocationBuilder().UseSpecialFolder(folder, enabled);

    /// <inheritdoc />
    public ILocationBuilder AddFilePath(string path) =>
        _saveLocationManager.MakeLocationBuilder().AddFilePath(path);
}
