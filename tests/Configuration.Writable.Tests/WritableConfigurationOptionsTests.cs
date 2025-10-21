using System;
using System.IO;
using System.Threading;

namespace Configuration.Writable.Tests;

public class WritableConfigurationOptionsBuilderTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
    }

    [Fact]
    public void ConfigFilePath_WithDefaultSettings_ShouldUseDefaultFileName()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings>();

        var path = options.ConfigFilePath;
        Path.GetFileName(path).ShouldBe("usersettings.json");
    }

    [Fact]
    public void ConfigFilePath_WithCustomFileName_ShouldUseCustomFileName()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings> { FilePath = "custom" };

        var path = options.ConfigFilePath;
        Path.GetFileName(path).ShouldBe("custom.json");
    }

    [Fact]
    public void SectionName_WithDefaultSettings_ShouldReturnEmpty()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings>();

        options.SectionName.ShouldBeEmpty();
    }

    [Fact]
    public void SectionName_WithInstanceName_ShouldReturnEmpty()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            InstanceName = "Instance1",
        };

        options.SectionName.ShouldBeEmpty();
    }

    [Fact]
    public void SectionName_WithEmptySectionName_ShouldReturnEmpty()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings> { SectionName = "" };

        options.SectionName.ShouldBeEmpty();
    }

    [Fact]
    public void SectionName_WithHierarchicalSectionName_ShouldSupportColonAndUnderscoreSeparators()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings>();

        options.SectionName = "App:Settings";
        options.SectionName.ShouldBe("App:Settings");

        options.SectionName = "Database__Connection";
        options.SectionName.ShouldBe("Database__Connection");

        options.SectionName = "App:Config__Settings";
        options.SectionName.ShouldBe("App:Config__Settings");
    }

    [Fact]
    public void ConfigFilePath_WithRelativePath_ShouldUseRuntimeFolderAsBase()
    {
        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            var options = new WritableConfigurationOptionsBuilder<TestSettings>
            {
                FilePath = "config/relative/test",
            };

            var expectedBasePath = AppContext.BaseDirectory;
            var expectedPath = Path.Combine(expectedBasePath, "config", "relative", "test.json");

            var actualPath = options.ConfigFilePath;
            actualPath.ShouldBe(expectedPath);

            // Use a deterministic temp directory to avoid CI environment issues
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                Directory.SetCurrentDirectory(tempDir);

                // Add small delay to ensure directory change is reflected in CI
                Thread.Sleep(50);

                var actualPathAfterCdChange = options.ConfigFilePath;
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
        var options = new WritableConfigurationOptionsBuilder<TestSettings> { FilePath = "test" };

        var configPath = options.UseExecutableDirectory();

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "test.json");
        configPath.ShouldBe(expectedPath);
        options.ConfigFilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public void UseExecutableDirectory_WithDefaultFileName_ShouldUseDefaultFileName()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings>();

        var configPath = options.UseExecutableDirectory();

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "usersettings.json");
        configPath.ShouldBe(expectedPath);
    }

    [Fact]
    public void UseCurrentDirectory_WithDefaultFileName_ShouldUseDefaultFileName()
    {
        var options = new WritableConfigurationOptionsBuilder<TestSettings>();

        var configPath = options.UseCurrentDirectory();

        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "usersettings.json");
        configPath.ShouldBe(expectedPath);
    }
}
