using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Configuration.Writable.Internal;

/// <summary>
/// Provides methods to get user-specific configuration file paths.
/// </summary>
internal static class UserConfigurationPath
{
    /// <summary>
    /// Get user config directory path for the current platform.
    /// </summary>
    public static string GetUserConfigRootDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: %APPDATA%
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: ~/Library/Application Support
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support"
            );
        }
        // Linux: XDG_CONFIG_HOME or ~/.config
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfig))
        {
            return xdgConfig;
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            ".config"
        );
    }
}
