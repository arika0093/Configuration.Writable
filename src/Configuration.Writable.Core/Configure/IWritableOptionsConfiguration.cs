using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Non-generic read-only view of a writable options configuration.
/// </summary>
public interface IWritableOptionsConfiguration
{
    /// <summary>
    /// The path to the configuration file.
    /// </summary>
    string ConfigFilePath { get; }

    /// <summary>
    /// The name of the options instance.
    /// </summary>
    string InstanceName { get; }

    /// <summary>
    /// The section name parts used to locate nested configuration values.
    /// </summary>
    List<string> SectionNameParts { get; }

    /// <summary>
    /// The source-generated schema metadata to persist with the options value.
    /// </summary>
    OptionsSchemaMetadata? SchemaMetadata { get; }

    /// <summary>Gets the base URI used for schema references in saved documents.</summary>
    string? SchemaBaseUri { get; }

    /// <summary>
    /// The debounce duration for change notifications.
    /// </summary>
    System.TimeSpan OnChangeDebounce { get; }

    /// <summary>
    /// An optional logger for diagnostics.
    /// </summary>
    ILogger? Logger { get; }
}
