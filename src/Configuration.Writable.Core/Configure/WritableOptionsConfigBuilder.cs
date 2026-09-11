#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Configuration.Writable.Abstractions;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Migration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#if NET
using System.Runtime.CompilerServices;
#endif

namespace Configuration.Writable.Configure;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public class WritableOptionsConfigBuilder<T> : WritableOptionsConfigBuilder
    where T : class, new()
{
    /// <inheritdoc />
    public new FormatProvider.IWritableFormatProvider FormatProvider
    {
        get => base.FormatProvider;
        set => base.FormatProvider = value;
    }

    /// <inheritdoc />
    public new IWritableFileProvider? FileProvider
    {
        get => base.FileProvider;
        set => base.FileProvider = value;
    }

    /// <inheritdoc />
    public new string? FilePath
    {
        get => base.FilePath;
        set => base.FilePath = value;
    }

    /// <inheritdoc />
    public new TimeSpan OnChangeDebounce
    {
        get => base.OnChangeDebounce;
        set => base.OnChangeDebounce = value;
    }

    /// <inheritdoc />
    public new bool RegisterAsSingleton
    {
        get => base.RegisterAsSingleton;
        set => base.RegisterAsSingleton = value;
    }

    /// <inheritdoc />
    public new bool UseDataAnnotationsValidation
    {
        get => base.UseDataAnnotationsValidation;
#if NET
        [RequiresUnreferencedCode(
            "Data Annotations validation may require types that cannot be statically analyzed."
        )]
#endif
        set => base.UseDataAnnotationsValidation = value;
    }

    /// <inheritdoc />
    public new ILogger? Logger
    {
        get => base.Logger;
        set => base.Logger = value;
    }

    /// <inheritdoc />
    public new ConfigurationConflictResolution ConflictResolution
    {
        get => base.ConflictResolution;
        set => base.ConflictResolution = value;
    }

    /// <inheritdoc />
    public new string SectionName
    {
        get => base.SectionName;
        set => base.SectionName = value;
    }

    /// <inheritdoc />
    public new string? SchemaBaseUri
    {
        get => base.SchemaBaseUri;
        set => base.SchemaBaseUri = value;
    }

    /// <inheritdoc />
#if NET
    [RequiresUnreferencedCode(
        "The runtime JSON contract may require types that cannot be statically analyzed."
    )]
    [RequiresDynamicCode(
        "The runtime JSON contract may require runtime code generation and is not compatible with NativeAOT."
    )]
#endif
    public new void EnableJsonSchemaGeneration() => base.EnableJsonSchemaGeneration();

    /// <inheritdoc />
    public new void EnableJsonSchemaGeneration(IJsonTypeInfoResolver typeInfoResolver) =>
        base.EnableJsonSchemaGeneration(typeInfoResolver);

    /// <inheritdoc />
    public new void EnablePromoteSaveLocation(bool enabled = true) =>
        base.EnablePromoteSaveLocation(enabled);

    /// <inheritdoc />
    public new void UseFile(string? path) => base.UseFile(path);

    /// <inheritdoc />
    public new void AddFilePath(string path, int priority = 0) => base.AddFilePath(path, priority);

    /// <inheritdoc />
    public new ILocationBuilder UseStandardSaveDirectory(
        string applicationId,
        bool enabled = true
    ) => base.UseStandardSaveDirectory(applicationId, enabled);

    /// <inheritdoc />
    public new ILocationBuilder UseExecutableDirectory() => base.UseExecutableDirectory();

    /// <inheritdoc />
    public new ILocationBuilder UseCurrentDirectory() => base.UseCurrentDirectory();

    /// <inheritdoc />
    public new ILocationBuilder UseSpecialFolder(Environment.SpecialFolder folder) =>
        base.UseSpecialFolder(folder);

    /// <inheritdoc />
    public new ILocationBuilder UseCustomDirectory(string directoryPath) =>
        base.UseCustomDirectory(directoryPath);

    private const string AotJsonReason =
        "JsonSerializerOptions.TypeInfoResolver handles NativeAOT scenarios";

#if NET
    private const string AotAnnotationsReason =
        "Data Annotations validation is disabled by default when dynamic code is not supported.";
#endif

    private Func<T, T>? _cloneMethod = null;
    private bool _usesDefaultJsonCloneFallback;
    private readonly List<Func<T, ValidateOptionsResult>> _validators = [];
    private readonly List<MigrationStep> _migrationSteps = [];

    /// <summary>
    /// Gets or sets a instance of <see cref="IWritableFormatProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="JsonFormatProvider"/> which uses JSON format. <br/>
    /// </summary>
    /// <summary>
    /// Gets or sets a instance of <see cref="IWritableFileProvider"/> used to handle the file writing operations override from provider's default.
    /// </summary>
    /// <summary>
    /// Gets or sets the path of the file used to store user settings. <br/>
    /// Defaults(null) to "usersettings" or InstanceName if specified. <br/>
    /// Extension is determined by the <see cref="IWritableFormatProvider"/> so it can be omitted.
    /// </summary>
    /// <summary>
    /// Gets or sets the debounce duration for change events.
    /// This delays event firing until rapid changes have stopped. <br/>
    /// Defaults to 300 ms.
    /// </summary>
    /// <summary>
    /// Indicates whether to automatically register <typeparamref name="T"/> as a singleton in the DI container. Defaults to false. <br/>
    /// Enabling this allows you to obtain the instance directly from the DI container,
    /// which is convenient, but automatic value updates are not provided, so be careful with the lifecycle. <br/>
    /// if you specify InstanceName, you can get it with [FromKeyedServices("instance-name")].
    /// </summary>
    /// <summary>
    /// Gets or sets a value indicating whether validation using data annotation attributes is enabled.
    /// Defaults to true when dynamic code is supported; otherwise false, including NativeAOT. <br/>
    /// If you want to use Source-Generator based validation or custom validation only, set this to false.
    /// </summary>
    /// <summary>
    /// Gets or sets the logger for configuration operations. Defaults to null. <br/>
    /// If null, logging is disabled or use provider's default logger.
    /// </summary>
    /// <summary>
    /// Gets or sets how saves handle changes made to the configuration file after it was loaded.
    /// Defaults to <see cref="ConfigurationConflictResolution.FailOnConflict"/>.
    /// </summary>
    /// <summary>
    /// Get or sets the name of the configuration section. <br/>
    /// You can use ":" or "__" to specify nested sections, e.g. "Parent:Child". <br/>
    /// If empty that means the root of the configuration file.
    /// </summary>
    internal WritableOptionsConfigBuilder(WritableOptionsConfigBuilder shared)
    {
        CopyFrom(shared);
    }

    /// <summary>Creates a builder with default settings.</summary>
#if NET
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The default JSON provider is retained for non-AOT applications; AOT callers can replace it with JsonAotFormatProvider."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The default JSON provider is retained for non-AOT applications; AOT callers can replace it with JsonAotFormatProvider."
    )]
#endif
    public WritableOptionsConfigBuilder() { }

    /// <summary>
    /// Sets the cloning strategy to use the default deep clone method if <typeparamref name="T"/> implements <see cref="IDeepCloneable{T}"/>.
    /// If not, it falls back to JSON serialization for deep cloning.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The JSON fallback is only selected when generated cloning is unavailable."
    )]
    public void UseDefaultCloneStrategy()
    {
        if (typeof(IDeepCloneable<T>).IsAssignableFrom(typeof(T)))
        {
            _cloneMethod = value => ((IDeepCloneable<T>)value).DeepClone();
        }
        else
        {
            UseJsonCloneStrategy();
            _usesDefaultJsonCloneFallback = true;
        }
    }

    /// <summary>
    /// Sets the cloning strategy to use JSON serialization for deep cloning of the configuration object.
    /// </summary>
    [RequiresUnreferencedCode("Default JSON serialization may not be compatible with NativeAOT.")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    public void UseJsonCloneStrategy()
    {
        _usesDefaultJsonCloneFallback = false;
        _cloneMethod = value =>
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(value);
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
        _usesDefaultJsonCloneFallback = false;
        _cloneMethod = value =>
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(value, jsonTypeInfo);
            return JsonSerializer.Deserialize<T>(json, jsonTypeInfo)!;
        };
    }

    /// <summary>
    /// Sets a custom cloning strategy for deep cloning of the configuration object.
    /// </summary>
    /// <param name="cloneStrategy">A function that defines the cloning strategy.</param>
    public void UseCustomCloneStrategy(Func<T, T> cloneStrategy)
    {
        _usesDefaultJsonCloneFallback = false;
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
    /// Creates a new instance of writable configuration options for the specified type.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
    public WritableOptionsConfiguration<T> BuildOptions(string instanceName)
    {
        var fileProvider = FileProvider ?? new CommonFileProvider();
        var configFilePath = SaveLocationManager.Build(
            FormatProvider,
            fileProvider,
            instanceName,
            PromoteSaveLocationEnabled
        );
        var readFilePath = PromoteSaveLocationEnabled
            ? SaveLocationManager.BuildReadPath(
                FormatProvider,
                fileProvider,
                instanceName,
                PromoteSaveLocationEnabled
            ) ?? configFilePath
            : configFilePath;
        var validator = BuildValidator();
        var schemaMetadata = OptionsMetadataResolver.Resolve<T>();
        if (schemaMetadata?.ModelId is not null && schemaMetadata.Version is not null)
        {
            GeneratedOptionsSchemaRegistry.Register(
                typeof(T),
                schemaMetadata.ModelId,
                schemaMetadata.Version.Value
            );
        }
        if (schemaMetadata is not null && !SupportsSchemaMetadata(FormatProvider))
        {
            throw new InvalidOperationException(
                $"Format provider {FormatProvider.GetType().Name} does not support options schema metadata required by {typeof(T).Name}."
            );
        }

        var migrationSteps = new List<MigrationStep>(_migrationSteps);
        var generatedMetadata = new T() as IGeneratedOptionsMetadata;
        generatedMetadata?.RegisterMigrations(new OptionsMigrationRegistrar(migrationSteps));
        ValidateMigrationSteps(schemaMetadata, migrationSteps);
        var sectionNamePart = SectionName
            .Split([":", "__"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var cloneMethod = GetCloneMethod();
        if (_usesDefaultJsonCloneFallback)
        {
            const string message =
                "Configuration.Writable is using JSON serialization as the default clone strategy. Configure a custom clone strategy for better performance and NativeAOT compatibility.";
            if (Logger != null)
            {
                Logger.LogWarning(message);
            }
            else
            {
                Console.Error.WriteLine(message);
            }
        }

        return new WritableOptionsConfiguration<T>
        {
            FormatProvider = FormatProvider,
            FileProvider = fileProvider,
            ConfigFilePath = configFilePath,
            ReadFilePath = readFilePath,
            PromoteSaveLocationEnabled = PromoteSaveLocationEnabled,
            InstanceName = instanceName,
            SectionNameParts = sectionNamePart,
            SchemaMetadata = schemaMetadata,
            SchemaBaseUri = SchemaBaseUri,
            OnChangeDebounce = OnChangeDebounce,
            ConflictResolution = ConflictResolution,
            CloneMethod = cloneMethod,
            Logger = Logger,
            Validator = validator,
            MigrationSteps = migrationSteps,
            MigrationLookup =
                migrationSteps.Count == 0 || schemaMetadata?.Version is null
                    ? null
                    : new MigrationLookup(typeof(T), schemaMetadata, migrationSteps),
        };
    }

    private static bool SupportsSchemaMetadata(
        FormatProvider.IWritableFormatProvider formatProvider
    ) =>
        formatProvider is FallbackFormatProvider fallbackProvider
            ? fallbackProvider.SupportsSchemaMetadata
            : formatProvider is IOptionsSchemaMetadataProvider;

    private static void ValidateMigrationSteps(
        OptionsSchemaMetadata? targetMetadata,
        IReadOnlyList<MigrationStep> migrationSteps
    )
    {
        if (migrationSteps.Count == 0)
        {
            return;
        }

        var targetVersion =
            targetMetadata?.Version
            ?? throw new InvalidOperationException(
                $"Target type {typeof(T).Name} does not declare a schema version."
            );

        if (migrationSteps.Any(step => step.ToVersion > targetVersion))
        {
            var step = migrationSteps.First(step => step.ToVersion > targetVersion);
            throw new InvalidOperationException(
                $"Migration to version {step.ToVersion} exceeds target version {targetVersion}."
            );
        }
    }

    private Func<T, T> GetCloneMethod()
    {
        if (_cloneMethod == null)
        {
            UseDefaultCloneStrategy();
        }

        return _cloneMethod
            ?? throw new InvalidOperationException("The clone strategy could not be initialized.");
    }

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
