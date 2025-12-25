using System;
using System.Collections.Generic;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a single migration step from one configuration version to another.
/// </summary>
public record MigrationStep
{
    /// <summary>
    /// Gets the source type that will be migrated from.
    /// </summary>
    public required Type FromType { get; init; }

    /// <summary>
    /// Gets the target type that will be migrated to.
    /// </summary>
    public required Type ToType { get; init; }

    /// <summary>
    /// Gets the migration function that transforms an instance of FromType to ToType.
    /// The function takes an object (which should be of type FromType) and returns an object (of type ToType).
    /// </summary>
    public required Func<object, object> MigrationFunc { get; init; }
}

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableOptionsConfiguration<T>
    where T : class, new()
{
    /// <summary>
    /// Gets or sets a instance of <see cref="IFormatProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="JsonFormatProvider"/> which uses JSON format. <br/>
    /// </summary>
    public required FormatProvider.IFormatProvider FormatProvider { get; init; }

    /// <summary>
    /// Gets or sets a instance of <see cref="IFileProvider"/> used to handle the file writing operations.
    /// </summary>
    public required IFileProvider FileProvider { get; init; }

    /// <summary>
    /// Gets the full file path to the configuration file, combining config folder and file name.
    /// </summary>
    public required string ConfigFilePath { get; init; }

    /// <summary>
    /// Gets or sets the name of the configuration instance. Defaults to Options.DefaultName ("").
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// Gets the parts of the section name split by ':' and '__' for hierarchical navigation.
    /// If empty, that means the root of the configuration file.
    /// </summary>
    public required List<string> SectionNameParts { get; init; }

    /// <summary>
    /// Gets or sets the throttle duration in milliseconds for change events.
    /// This helps to prevent excessive event firing during rapid changes.
    /// </summary>
    public required int OnChangeThrottleMs { get; init; }

    /// <summary>
    /// Gets or sets the cloning strategy function to create deep copies of the configuration object.
    /// </summary>
    public required Func<T, T> CloneMethod { get; init; }

    /// <summary>
    /// Gets or sets the logger for configuration operations.
    /// If null, logging is disabled. Defaults to null.
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Gets or sets the validation function to be executed before saving configuration.
    /// If null, no validation is performed. Defaults to null.
    /// </summary>
    public Func<T, ValidateOptionsResult>? Validator { get; init; }

    /// <summary>
    /// Gets the list of migration steps to apply when loading configuration from older versions.
    /// The migrations are applied in the order they are defined (e.g., V1 -> V2 -> V3).
    /// </summary>
    public List<MigrationStep> MigrationSteps { get; init; } = [];
}
