using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Configuration.Writable.Configure;

/// <summary>
/// Provides methods to get user-specific configuration file paths.
/// </summary>
internal static class StandardSaveLocationUtility
{
    /// <summary>
    /// Get user config directory path for the current platform.
    /// </summary>
    public static string GetConfigDirectory()
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
        // in macOS or Linux: if XDG_CONFIG_HOME is set, use it
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfig))
        {
            return xdgConfig;
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
        else
        {
            // Linux: ~/.config
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                ".config"
            );
        }
#endif
    }
}
