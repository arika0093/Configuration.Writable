#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Migration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Configure;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableOptionsConfigBuilder<T>
    where T : class, new()
{
#if NET
    private const string AotJsonReason =
        "JsonSerializerOptions.TypeInfoResolver handles NativeAOT scenarios";

    private const string AotAnnotationsReason =
        "Data Annotations validation may not be compatible with NativeAOT. You can disable it by setting UseDataAnnotationsValidation to false.";
#endif

    private const string DefaultSectionName = "";
    private Func<T, T>? _cloneMethod = null;
    private readonly List<Func<T, ValidateOptionsResult>> _validators = [];
    private readonly SaveLocationManager _saveLocationManager = new();
    private readonly List<MigrationStep> _migrationSteps = [];

    /// <summary>
    /// Gets or sets a instance of <see cref="IFormatProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="JsonFormatProvider"/> which uses JSON format. <br/>
    /// </summary>
    public FormatProvider.IFormatProvider FormatProvider { get; set; } = new JsonFormatProvider();

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
        get => _saveLocationManager.LocationPath;
        set => UseFile(value);
    }

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
    /// Sets the cloning strategy to use JSON serialization for deep cloning of the configuration object.
    /// </summary>
#if NET
    [RequiresUnreferencedCode("Default JSON serialization may not be compatible with NativeAOT.")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
#endif
    public void UseJsonCloneStrategy()
    {
        _cloneMethod = value =>
        {
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(json)!;
        };
    }

    /// <summary>
    /// Sets the cloning strategy to use JSON serialization for deep cloning of the configuration object.
    /// This overload allows specifying custom JsonTypeInfo for serialization.
    /// </summary>
    /// <param name="jsonTypeInfo">The JsonTypeInfo to use for serialization and deserialization.</param>
    public void UseJsonCloneStrategy(JsonTypeInfo<T> jsonTypeInfo)
    {
        _cloneMethod = value =>
        {
            var json = JsonSerializer.Serialize(value, jsonTypeInfo);
            return JsonSerializer.Deserialize<T>(json, jsonTypeInfo)!;
        };
    }

    /// <summary>
    /// Sets a custom cloning strategy for deep cloning of the configuration object.
    /// </summary>
    /// <param name="cloneStrategy">A function that defines the cloning strategy.</param>
    public void UseCustomCloneStrategy(Func<T, T> cloneStrategy)
    {
        _cloneMethod = cloneStrategy;
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
    /// Registers a migration step from an old configuration version to a new version.
    /// Migrations should be registered in sequential order (e.g., V1 -> V2, then V2 -> V3).
    /// </summary>
    /// <typeparam name="TOld">The old configuration type. Must implement <see cref="IHasVersion"/>.</typeparam>
    /// <typeparam name="TNew">The new configuration type. Must implement <see cref="IHasVersion"/>.</typeparam>
    /// <param name="migrator">A function that converts an instance of <typeparamref name="TOld"/> to <typeparamref name="TNew"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown when attempting to register a downgrade migration (where the new version is less than the old version).</exception>
    public void UseMigration<TOld, TNew>(Func<TOld, TNew> migrator)
        where TOld : class, IHasVersion, new()
        where TNew : class, IHasVersion, new()
    {
        // Validate that this is not a downgrade
        var oldVersion = VersionCache.GetVersion<TOld>();
        var newVersion = VersionCache.GetVersion<TNew>();

        if (newVersion <= oldVersion)
        {
            var oldName = typeof(TOld).Name;
            var newName = typeof(TNew).Name;
            throw new InvalidOperationException($"""
                Migration downgrade detected: Cannot migrate from version {oldVersion} ({oldName}) to version {newVersion} ({newName}).
                Migrations must move to a higher version number.
                If you need to revert to a previous schema, increase the version number and implement logic to convert to the older format.
                """
            );
        }

        _migrationSteps.Add(new MigrationStep<TOld, TNew>(migrator));
    }

    /// <summary>
    /// Creates a new instance of writable configuration options for the specified type.
    /// </summary>
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    public WritableOptionsConfiguration<T> BuildOptions(string instanceName)
    {
        var fileProvider = FileProvider ?? new CommonFileProvider();
        var configFilePath = _saveLocationManager.Build(FormatProvider, fileProvider, instanceName);
        var validator = BuildValidator();
        var sectionNamePart = SectionName
            .Split([":", "__"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (_cloneMethod == null)
        {
            UseJsonCloneStrategy();
        }

        return new WritableOptionsConfiguration<T>
        {
            FormatProvider = FormatProvider,
            FileProvider = fileProvider,
            ConfigFilePath = configFilePath,
            InstanceName = instanceName,
            SectionNameParts = sectionNamePart,
            OnChangeThrottleMs = OnChangeThrottleMs,
            CloneMethod = _cloneMethod!,
            Logger = Logger,
            Validator = validator,
            MigrationSteps = [.. _migrationSteps],
        };
    }

    /// <summary>
    /// Use a specific file path for saving the configuration.
    /// </summary>
    /// <param name="path">The file path to use.</param>
    public void UseFile(string? path)
    {
        _saveLocationManager.LocationBuilders.Clear();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _saveLocationManager.MakeLocationBuilder().AddFilePath(path!);
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
    /// <param name="enabled">If false, alternatively uses the executable directory.</param>
    public ILocationBuilder UseStandardSaveDirectory(string applicationId, bool enabled = true)
    {
        var builder = _saveLocationManager.MakeLocationBuilder();
        if (enabled)
        {
            builder.UseStandardSaveDirectory(applicationId);
        }
        else
        {
            builder.UseExecutableDirectory();
        }
        return builder;
    }

    /// <summary>
    /// Sets the configuration folder to the directory where the executable is located. (default behavior)
    /// </summary>
    /// <remarks>
    /// This uses <see cref="AppContext.BaseDirectory"/> to determine the executable directory.
    /// </remarks>
    public ILocationBuilder UseExecutableDirectory() =>
        _saveLocationManager.MakeLocationBuilder().UseExecutableDirectory();

    /// <summary>
    /// Sets the configuration folder to the current working directory.
    /// </summary>
    /// <remarks>
    /// This uses <see cref="System.IO.Directory.GetCurrentDirectory()"/> to determine the current directory.
    /// </remarks>
    public ILocationBuilder UseCurrentDirectory() =>
        _saveLocationManager.MakeLocationBuilder().UseCurrentDirectory();

    /// <summary>
    /// Sets the configuration folder to a special folder defined by <see cref="Environment.SpecialFolder"/>.
    /// </summary>
    /// <param name="folder">The special folder to use as the configuration folder.</param>
    public ILocationBuilder UseSpecialFolder(Environment.SpecialFolder folder) =>
        _saveLocationManager.MakeLocationBuilder().UseSpecialFolder(folder);

    /// <summary>
    /// Sets the configuration folder to a custom folder path.
    /// </summary>
    /// <param name="directoryPath">The custom directory path to use as the configuration folder.</param>
    public ILocationBuilder UseCustomDirectory(string directoryPath) =>
        _saveLocationManager.MakeLocationBuilder().UseCustomDirectory(directoryPath);

    /// <summary>
    /// Builds the composite validator from all registered validators.
    /// </summary>
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotAnnotationsReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotAnnotationsReason)]
#endif
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
#if NET
    [RequiresUnreferencedCode("Data Annotations validation may not be compatible with NativeAOT.")]
#endif
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
}
