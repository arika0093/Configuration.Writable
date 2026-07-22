using System.Collections.Generic;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Non-generic read-only view of a writable options configuration.
/// </summary>
public interface IWritableOptionsConfiguration
{
    /// <summary>
    /// The file provider used to read and write the configuration file.
    /// </summary>
    IWritableFileProvider FileProvider { get; }

    /// <summary>
    /// The format provider used to serialize and deserialize the configuration.
    /// </summary>
    IWritableFormatProvider FormatProvider { get; }

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
    /// The throttle duration for change notifications.
    /// </summary>
    System.TimeSpan OnChangeThrottle { get; }

    /// <summary>
    /// An optional logger for diagnostics.
    /// </summary>
    ILogger? Logger { get; }
}
