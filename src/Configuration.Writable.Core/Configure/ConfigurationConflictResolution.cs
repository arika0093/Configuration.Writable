namespace Configuration.Writable.Configure;

/// <summary>
/// Specifies how a save handles changes made to a configuration file after it was loaded.
/// </summary>
public enum ConfigurationConflictResolution
{
    /// <summary>
    /// Reject the save when the loaded file fingerprint no longer matches the file on disk.
    /// </summary>
    FailOnConflict = 0,

    /// <summary>
    /// Save the configuration even when the file changed after it was loaded.
    /// </summary>
    LastWriteWins = 1,
}
