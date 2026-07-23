using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Configuration.Writable.Configure;

namespace Configuration.Writable.Tests;

public class WritableOptionsConfigBuilderTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
    }

    [Fact]
    public void ConfigFilePath_WithDefaultSettings_ShouldUseDefaultFileName()
    {
        var options = new WritableOptionsConfigBuilder<TestSettings>();

        var path = options.BuildOptions("").ConfigFilePath;
        Path.GetFileName(path).ShouldBe("usersettings.json");
    }

    [Fact]
    public void ConfigFilePath_WithCustomFileName_ShouldUseCustomFileName()
    {
        var options = new WritableOptionsConfigBuilder<TestSettings> { FilePath = "custom" };

        var path = options.BuildOptions("").ConfigFilePath;
        Path.GetFileName(path).ShouldBe("custom.json");
    }

    [Fact]
    public void SectionName_WithDefaultSettings_ShouldReturnEmpty()
    {
        var options = new WritableOptionsConfigBuilder<TestSettings>();

        options.SectionName.ShouldBeEmpty();
    }

    [Fact]
    public void UseDataAnnotationsValidation_WithDefaultSettings_ShouldMatchDynamicCodeSupport()
    {
        var options = new WritableOptionsConfigBuilder<TestSettings>();

        options.UseDataAnnotationsValidation.ShouldBe(RuntimeFeature.IsDynamicCodeSupported);
    }

    [Fact]
    public void ConfigFilePath_WithRelativePath_ShouldUseRuntimeFolderAsBase()
    {
        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            var options = new WritableOptionsConfigBuilder<TestSettings>
            {
                FilePath = "config/relative/test",
            };

            var expectedBasePath = AppContext.BaseDirectory;
            var expectedPath = Path.Combine(expectedBasePath, "config", "relative", "test.json");

            var actualPath = options.BuildOptions("").ConfigFilePath;
            actualPath.ShouldBe(expectedPath);

            // Use a deterministic temp directory to avoid CI environment issues
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                Directory.SetCurrentDirectory(tempDir);

                // Add small delay to ensure directory change is reflected in CI
                Thread.Sleep(50);

                var actualPathAfterCdChange = options.BuildOptions("").ConfigFilePath;
                actualPathAfterCdChange.ShouldBe(expectedPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    [Fact]
    public void UseExecutableDirectory_ShouldSetConfigFolderToBaseDirectory()
    {
        var options = new WritableOptionsConfigBuilder<TestSettings>();
        options.UseExecutableDirectory().AddFilePath("test");

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "test.json");
        options.BuildOptions("").ConfigFilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public void UseCurrentDirectory_ShouldSetConfigFolderToBaseDirectory()
    {
        var options = new WritableOptionsConfigBuilder<TestSettings>();
        options.UseCurrentDirectory().AddFilePath("usersettings");

        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "usersettings.json");
        options.BuildOptions("").ConfigFilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public void UseCustomDirectory_WithNonExistentDirectory_ShouldCreateDirectoryAndResolvePath()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var options = new WritableOptionsConfigBuilder<TestSettings>();
            options.UseCustomDirectory(testDir).AddFilePath("testsettings");

            Directory.Exists(testDir).ShouldBeFalse();

            // BuildOptions should create the directory if it doesn't exist
            var configPath = options.BuildOptions("").ConfigFilePath;

            // After BuildOptions, the directory should exist
            Directory.Exists(testDir).ShouldBeTrue();
            configPath.ShouldBe(Path.Combine(testDir, "testsettings.json"));
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void UseStandardSaveDirectory_WithNonExistentDirectory_ShouldCreateDirectoryAndResolvePath()
    {
        // Use a unique application ID that should not exist
        var appId = $"ConfigurationWritableTest_{Guid.NewGuid():N}";
        var expectedDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appId
        );
        try
        {
            var options = new WritableOptionsConfigBuilder<TestSettings>();
            options.UseStandardSaveDirectory(appId).AddFilePath("testsettings");

            // BuildOptions should create the directory if it doesn't exist
            var configPath = options.BuildOptions("").ConfigFilePath;

            // After BuildOptions, the directory should exist
            Directory.Exists(expectedDir).ShouldBeTrue();
            configPath.ShouldBe(Path.Combine(expectedDir, "testsettings.json"));
        }
        finally
        {
            if (Directory.Exists(expectedDir))
            {
                Directory.Delete(expectedDir, true);
            }
        }
    }
}
