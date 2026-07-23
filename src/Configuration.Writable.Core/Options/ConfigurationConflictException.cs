using System;

namespace Configuration.Writable.Configure;

/// <summary>
/// The exception that is thrown when a configuration file changed after it was loaded and before it could be saved.
/// </summary>
public sealed class ConfigurationConflictException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationConflictException"/> class.
    /// </summary>
    /// <param name="configFilePath">The path of the configuration file that changed.</param>
    public ConfigurationConflictException(string configFilePath)
        : base(
            $"Configuration file '{configFilePath}' changed after it was loaded. Reload the configuration before saving, or set ConflictResolution to LastWriteWins."
        )
    {
        ConfigFilePath = configFilePath;
    }

    /// <summary>
    /// Gets the path of the configuration file that changed.
    /// </summary>
    public string ConfigFilePath { get; }
}
