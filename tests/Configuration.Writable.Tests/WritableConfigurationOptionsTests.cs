using System;
using System.IO;

namespace Configuration.Writable.Tests;

public class WritableConfigurationOptionsTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
    }

    [Fact]
    public void ConfigFilePath_WithDefaultSettings_ShouldUseDefaultFileName()
    {
        var options = new WritableConfigurationOptions<TestSettings>();

        var path = options.ConfigFilePath;
        Path.GetFileName(path).ShouldBe("usersettings.json");
    }

    [Fact]
    public void ConfigFilePath_WithCustomFileName_ShouldUseCustomFileName()
    {
        var options = new WritableConfigurationOptions<TestSettings> { FilePath = "custom" };

        var path = options.ConfigFilePath;
        Path.GetFileName(path).ShouldBe("custom.json");
    }

    [Fact]
    public void SectionName_WithDefaultSettings_ShouldReturnSectionRootName()
    {
        var options = new WritableConfigurationOptions<TestSettings>();

        options.SectionName.ShouldBe("UserSettings");
    }

    [Fact]
    public void SectionName_WithInstanceName_ShouldCombineNames()
    {
        var options = new WritableConfigurationOptions<TestSettings> { InstanceName = "Instance1" };

        options.SectionName.ShouldBe("UserSettings-Instance1");
    }

    [Fact]
    public void SectionName_WithEmptySectionRootName_ShouldReturnEmpty()
    {
        var options = new WritableConfigurationOptions<TestSettings> { SectionRootName = "" };

        options.SectionName.ShouldBeEmpty();
    }
}
