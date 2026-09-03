using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

/// <summary>
/// Provides configuration metadata for the default options instance.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
[SuppressMessage(
    "Major Code Smell",
    "S2326",
    Justification = "The generic parameter provides type-safe DI service identity."
)]
public interface IOptionsConfigurationAccessor<T>
    where T : class, new()
{
    /// <summary>
    /// Gets configuration metadata for the default options instance.
    /// </summary>
    IOptionsConfigurationInfo GetConfigurationInfo();
}

/// <summary>
/// Provides configuration metadata for named options instances.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
[SuppressMessage(
    "Major Code Smell",
    "S2326",
    Justification = "The generic parameter provides type-safe DI service identity."
)]
public interface INamedOptionsConfigurationAccessor<T>
    where T : class, new()
{
    /// <summary>
    /// Gets configuration metadata for the specified options instance.
    /// </summary>
    /// <param name="name">The name of the options instance.</param>
    IOptionsConfigurationInfo GetConfigurationInfo(string name);
}
