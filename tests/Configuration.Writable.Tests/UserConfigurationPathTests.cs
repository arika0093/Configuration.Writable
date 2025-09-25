using System;
using System.IO;
using System.Runtime.InteropServices;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

public class UserConfigurationPathTests
{
    [FactOnWindows]
    public void GetUserConfigRootDirectory_OnWindows_ShouldReturnAppData()
    {
        var path = UserConfigurationPath.GetUserConfigRootDirectory();
        var expectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        path.ShouldBe(expectedPath);
        path.ShouldContain("AppData");
    }

    [FactOnMacOS]
    public void GetUserConfigRootDirectory_OnMacOS_WithXDGConfigHome_ShouldReturnXDGPath()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var testXdgPath = "/tmp/test_xdg_config";

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", testXdgPath);

            var path = UserConfigurationPath.GetUserConfigRootDirectory();

            path.ShouldBe(testXdgPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdgConfig);
        }
    }

    [FactOnMacOS]
    public void GetUserConfigRootDirectory_OnMacOS_WithoutXDGConfigHome_ShouldReturnLibraryApplicationSupport()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

            var path = UserConfigurationPath.GetUserConfigRootDirectory();
            var expectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support"
            );

            path.ShouldBe(expectedPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdgConfig);
        }
    }

    [FactOnLinux]
    public void GetUserConfigRootDirectory_OnLinux_WithXDGConfigHome_ShouldReturnXDGPath()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var testXdgPath = "/tmp/test_xdg_config";

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", testXdgPath);

            var path = UserConfigurationPath.GetUserConfigRootDirectory();

            path.ShouldBe(testXdgPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdgConfig);
        }
    }

    [FactOnLinux]
    public void GetUserConfigRootDirectory_OnLinux_WithoutXDGConfigHome_ShouldReturnDotConfig()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

            var path = UserConfigurationPath.GetUserConfigRootDirectory();
            var expectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                ".config"
            );

            path.ShouldBe(expectedPath);
            path.ShouldEndWith(".config");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdgConfig);
        }
    }

    [Fact]
    public void GetUserConfigRootDirectory_ConsistentResults_ShouldReturnSamePathOnMultipleCalls()
    {
        var path1 = UserConfigurationPath.GetUserConfigRootDirectory();
        var path2 = UserConfigurationPath.GetUserConfigRootDirectory();

        path1.ShouldBe(path2);
    }
}
