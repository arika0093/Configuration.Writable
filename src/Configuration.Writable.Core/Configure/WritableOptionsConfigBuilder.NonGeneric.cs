using System;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Logging;
#if NET
using System.Runtime.CompilerServices;
#endif

namespace Configuration.Writable.Configure;

/// <summary>Configures settings shared by one or more writable options registrations.</summary>
public class WritableOptionsConfigBuilder
{
    internal SaveLocationManager SaveLocationManager { get; private set; } = new();

    /// <summary>Gets or sets the format provider.</summary>
    public IWritableFormatProvider FormatProvider { get; set; } = new JsonFormatProvider();

    /// <summary>Gets or sets the file provider.</summary>
    public IWritableFileProvider? FileProvider { get; set; }

    /// <summary>Gets or sets the configured file path.</summary>
    public string? FilePath
    {
        get => SaveLocationManager.LocationPath;
        set => UseFile(value);
    }

    /// <summary>Gets or sets the debounce duration for change events.</summary>
    public TimeSpan OnChangeDebounce { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Gets or sets whether the options value is registered as a singleton.</summary>
    public bool RegisterAsSingleton { get; set; }

    /// <summary>Gets or sets whether Data Annotations validation is enabled.</summary>
    public bool UseDataAnnotationsValidation { get; set; } =
#if NET
        RuntimeFeature.IsDynamicCodeSupported;
#else
        false;
#endif

    /// <summary>Gets or sets the logger.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>Gets or sets conflict resolution behavior.</summary>
    public ConfigurationConflictResolution ConflictResolution { get; set; } =
        ConfigurationConflictResolution.FailOnConflict;

    /// <summary>Gets or sets the configuration section name.</summary>
    public string SectionName { get; set; } = "";

    /// <summary>Gets or sets the base URI used for schema references in saved documents.</summary>
    public string? SchemaBaseUri { get; set; }

    internal bool JsonSchemaGenerationEnabled { get; private set; }
    internal IJsonTypeInfoResolver? JsonSchemaTypeInfoResolver { get; private set; }

    /// <summary>
    /// Gets a value indicating whether existing settings are promoted to the preferred save location during startup.
    /// </summary>
    public bool PromoteSaveLocationEnabled { get; private set; }

    internal void CopyFrom(WritableOptionsConfigBuilder source)
    {
        FormatProvider = source.FormatProvider is FallbackFormatProvider fallbackProvider
            ? fallbackProvider.Clone()
            : source.FormatProvider;
        FileProvider = source.FileProvider;
        OnChangeDebounce = source.OnChangeDebounce;
        RegisterAsSingleton = source.RegisterAsSingleton;
        UseDataAnnotationsValidation = source.UseDataAnnotationsValidation;
        Logger = source.Logger;
        ConflictResolution = source.ConflictResolution;
        SectionName = source.SectionName;
        SchemaBaseUri = source.SchemaBaseUri;
        JsonSchemaGenerationEnabled = source.JsonSchemaGenerationEnabled;
        JsonSchemaTypeInfoResolver = source.JsonSchemaTypeInfoResolver;
        PromoteSaveLocationEnabled = source.PromoteSaveLocationEnabled;
        SaveLocationManager = new SaveLocationManager(source.SaveLocationManager);
    }

    /// <summary>Enables command-line JSON schema generation using the runtime JSON contract.</summary>
    public void EnableJsonSchemaGeneration()
    {
        JsonSchemaGenerationEnabled = true;
        JsonSchemaTypeInfoResolver = null;
    }

    /// <summary>Enables command-line JSON schema generation using a JSON type-info resolver.</summary>
    public void EnableJsonSchemaGeneration(IJsonTypeInfoResolver typeInfoResolver)
    {
        JsonSchemaTypeInfoResolver =
            typeInfoResolver ?? throw new ArgumentNullException(nameof(typeInfoResolver));
        JsonSchemaGenerationEnabled = true;
    }

    /// <summary>
    /// Registers additional format providers that may be used to load an existing configuration
    /// when the canonical file for <see cref="FormatProvider"/> does not exist.
    /// A successfully loaded fallback configuration is promoted to the canonical format after schema migration.
    /// </summary>
    /// <param name="formatProviders">The fallback format providers, in resolution order.</param>
    public void AddFallbackFormatProvider(params IWritableFormatProvider[] formatProviders)
    {
        if (formatProviders is null)
        {
            throw new ArgumentNullException(nameof(formatProviders));
        }

        if (formatProviders.Any(formatProvider => formatProvider is null))
        {
            throw new ArgumentNullException(nameof(formatProviders));
        }

        if (formatProviders.Length == 0)
        {
            return;
        }

        if (FormatProvider is not FallbackFormatProvider fallbackProvider)
        {
            fallbackProvider = new FallbackFormatProvider(FormatProvider);
            FormatProvider = fallbackProvider;
        }

        fallbackProvider.AddFallbacks(formatProviders);
    }

    /// <summary>
    /// Enables or disables promotion of an existing configuration file to the preferred save location during startup.
    /// </summary>
    public void EnablePromoteSaveLocation(bool enabled = true) =>
        PromoteSaveLocationEnabled = enabled;

    /// <summary>Uses a specific file path and clears all previously configured locations.</summary>
    public void UseFile(string? path)
    {
        SaveLocationManager.LocationBuilders.Clear();
        if (path == null)
            return;
        if (!string.IsNullOrWhiteSpace(path))
            SaveLocationManager.MakeLocationBuilder().AddFilePath(path);
    }

    /// <summary>Adds a file path to the most recently selected directory.</summary>
    public void AddFilePath(string path, int priority = 0)
    {
        var location =
            SaveLocationManager.LocationBuilders.Count == 0
                ? SaveLocationManager.MakeLocationBuilder()
                : (LocationBuilderInternal)SaveLocationManager.LocationBuilders[^1];
        location.AddFilePath(path, priority);
    }

    /// <summary>Uses the standard save directory.</summary>
    public ILocationBuilder UseStandardSaveDirectory(string applicationId, bool enabled = true)
    {
        var builder = SaveLocationManager.MakeLocationBuilder();
        return enabled
            ? builder.UseStandardSaveDirectory(applicationId)
            : builder.UseExecutableDirectory();
    }

    /// <summary>Uses the executable directory.</summary>
    public ILocationBuilder UseExecutableDirectory() =>
        SaveLocationManager.MakeLocationBuilder().UseExecutableDirectory();

    /// <summary>Uses the current directory.</summary>
    public ILocationBuilder UseCurrentDirectory() =>
        SaveLocationManager.MakeLocationBuilder().UseCurrentDirectory();

    /// <summary>Uses a special folder.</summary>
    public ILocationBuilder UseSpecialFolder(Environment.SpecialFolder folder) =>
        SaveLocationManager.MakeLocationBuilder().UseSpecialFolder(folder);

    /// <summary>Uses a custom directory.</summary>
    public ILocationBuilder UseCustomDirectory(string directoryPath) =>
        SaveLocationManager.MakeLocationBuilder().UseCustomDirectory(directoryPath);
}
