using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Configuration.Writable.Tests;

/// <summary>
/// Custom test attribute that only runs on Windows.
/// </summary>
public class FactOnWindowsAttribute : SkipAttribute
{
    public FactOnWindowsAttribute()
        : base("This test only runs on Windows") { }

    public override Task<bool> ShouldSkip(TestRegisteredContext testContext)
    {
        return Task.FromResult(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }
}

/// <summary>
/// Custom test attribute that only runs on macOS.
/// </summary>
public class FactOnMacOSAttribute : SkipAttribute
{
    public FactOnMacOSAttribute()
        : base("This test only runs on macOS") { }

    public override Task<bool> ShouldSkip(TestRegisteredContext testContext)
    {
        return Task.FromResult(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
    }
}

/// <summary>
/// Custom test attribute that only runs on Linux.
/// </summary>
public class FactOnLinuxAttribute : SkipAttribute
{
    public FactOnLinuxAttribute()
        : base("This test only runs on Linux") { }

    public override Task<bool> ShouldSkip(TestRegisteredContext testContext)
    {
        return Task.FromResult(!RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
    }
}
