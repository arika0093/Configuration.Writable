using System;
using System.Runtime.InteropServices;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Configuration.Writable.Tests;

/// <summary>
/// Custom fact attribute that only runs on Windows
/// </summary>
public class FactOnWindowsAttribute : FactAttribute
{
    public FactOnWindowsAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "This test only runs on Windows";
        }
    }
}

/// <summary>
/// Custom fact attribute that only runs on macOS
/// </summary>
public class FactOnMacOSAttribute : FactAttribute
{
    public FactOnMacOSAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Skip = "This test only runs on macOS";
        }
    }
}

/// <summary>
/// Custom fact attribute that only runs on Linux
/// </summary>
public class FactOnLinuxAttribute : FactAttribute
{
    public FactOnLinuxAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Skip = "This test only runs on Linux";
        }
    }
}
