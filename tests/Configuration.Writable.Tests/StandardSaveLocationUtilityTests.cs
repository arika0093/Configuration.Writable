using System;
using System.IO;
using Configuration.Writable.Configure;

namespace Configuration.Writable.Tests;

public class StandardSaveLocationUtilityTests
{
    [FactOnWindows]
    public void GetConfigDirectory_OnWindows_ShouldReturnAppData()
    {
        var path = StandardSaveLocationUtility.GetConfigDirectory();
        var expectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        path.ShouldBe(expectedPath);
        path.ShouldContain("AppData");
    }

    [FactOnMacOS]
    public void GetConfigDirectory_OnMacOS_WithXDGConfigHome_ShouldReturnXDGPath()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var testXdgPath = "/tmp/test_xdg_config";

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", testXdgPath);

            var path = StandardSaveLocationUtility.GetConfigDirectory();

            path.ShouldBe(testXdgPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdgConfig);
        }
    }

    [FactOnMacOS]
    public void GetConfigDirectory_OnMacOS_WithoutXDGConfigHome_ShouldReturnLibraryApplicationSupport()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

            var path = StandardSaveLocationUtility.GetConfigDirectory();
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
    public void GetConfigDirectory_OnLinux_WithXDGConfigHome_ShouldReturnXDGPath()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var testXdgPath = "/tmp/test_xdg_config";

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", testXdgPath);

            var path = StandardSaveLocationUtility.GetConfigDirectory();

            path.ShouldBe(testXdgPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdgConfig);
        }
    }

    [FactOnLinux]
    public void GetConfigDirectory_OnLinux_WithoutXDGConfigHome_ShouldReturnDotConfig()
    {
        var originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

            var path = StandardSaveLocationUtility.GetConfigDirectory();
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
    public void GetConfigDirectory_ConsistentResults_ShouldReturnSamePathOnMultipleCalls()
    {
        var path1 = StandardSaveLocationUtility.GetConfigDirectory();
        var path2 = StandardSaveLocationUtility.GetConfigDirectory();

        path1.ShouldBe(path2);
    }
}
