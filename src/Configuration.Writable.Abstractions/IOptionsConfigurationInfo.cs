using System.Collections.Generic;

namespace Configuration.Writable;

/// <summary>
/// Describes provider-independent metadata for an options configuration.
/// </summary>
public interface IOptionsConfigurationInfo
{
    /// <summary>
    /// Gets the configured options instance name.
    /// </summary>
    string InstanceName { get; }

    /// <summary>
    /// Gets the file path used to produce the current value.
    /// </summary>
    string ReadPath { get; }

    /// <summary>
    /// Gets the file path used by the next save operation.
    /// </summary>
    string WritePath { get; }

    /// <summary>
    /// Gets the format provider's default file extension without a leading period.
    /// </summary>
    string FormatFileExtension { get; }

    /// <summary>
    /// Gets the hierarchical configuration section name parts.
    /// </summary>
    IReadOnlyList<string> SectionNameParts { get; }
}
