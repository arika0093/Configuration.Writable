namespace Configuration.Writable;

/// <summary>
/// Interface for configuration classes that support versioning and migration.
/// Implement this interface to enable automatic migration of configuration data
/// when the schema changes across versions.
/// </summary>
public interface IHasVersion
{
    /// <summary>
    /// Gets the version number of this configuration schema.
    /// This value is used to determine which migrations need to be applied
    /// when loading configuration data from a file.
    /// </summary>
    int Version { get; }
}
