using System;
using System.Collections.Generic;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

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
    /// Gets or sets the name of the configuration section. Defaults to "UserSettings".
    /// If empty, that means the root of the configuration file.
    /// If use multiple configuration file for same type T, you must set different SectionName for each.
    /// </summary>
    public required string SectionName { get; init; }

    /// <summary>
    /// Gets or sets the parts of the section name split by ':' and '__' for hierarchical navigation.
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
    public required Func<T, T> CloneMethod { get; init; } // TODO: rename to CloneMethod

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
}
