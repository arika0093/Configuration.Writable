#pragma warning disable S2326 // Unused type parameters should be removed
using System;
using System.IO;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableConfigurationOptions<T>
    where T : class
{
    /// <summary>
    /// Gets or sets a instance of <see cref="IWritableConfigProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="WritableConfigJsonProvider"/> which uses JSON format. <br/>
    /// </summary>
    public required IWritableConfigProvider Provider { get; init; }

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
    /// Gets or sets the logger for configuration operations.
    /// If null, logging is disabled. Defaults to null.
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Gets or sets the validation function to be executed before saving configuration.
    /// If null, no validation is performed. Defaults to null.
    /// </summary>
    public Func<T, ValidationResult>? Validator { get; init; }
}
