using System;

namespace Configuration.Writable;

#pragma warning disable S1133 // Kept for backward compatibility.
/// <summary>
/// Interface for configuration classes that support versioning and migration.
/// Implement this interface to enable automatic migration of configuration data
/// when the schema changes across versions.
/// </summary>
[Obsolete("This interface is deprecated. Use [OptionsModel(Version = x)] instead.")]
public interface IHasVersion
{
    /// <summary>
    /// Gets the version number of this configuration schema.
    /// This value is used to determine which migrations need to be applied
    /// when loading configuration data from a file.
    /// </summary>
    int Version { get; set; }
}
#pragma warning restore S1133
