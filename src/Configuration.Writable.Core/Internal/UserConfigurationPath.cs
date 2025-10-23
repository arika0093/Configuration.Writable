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
#if NETFRAMEWORK
        // in .NET Framework, windows only
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: %APPDATA%
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: ~/Library/Application Support or $XDG_CONFIG_HOME
            if (!string.IsNullOrEmpty(xdgConfig))
            {
                return xdgConfig;
            }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support"
            );
        }
        else
        {
            // Linux: XDG_CONFIG_HOME or ~/.config
            if (!string.IsNullOrEmpty(xdgConfig))
            {
                return xdgConfig;
            }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                ".config"
            );
        }
#endif
    }
}
