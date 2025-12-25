using System;
using System.IO;
using System.Linq;
using Configuration.Writable.Configure;
using Configuration.Writable.FormatProvider;
using Shouldly;
using Xunit;

namespace Configuration.Writable.Yaml.Tests;

public class YamlMigrationSupportTests : IDisposable
{
    private readonly string _tempDirectory;

    public YamlMigrationSupportTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void LoadConfiguration_Yaml_ShouldApplyMultipleMigrations()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.yaml");
        File.WriteAllText(
            filePath,
            """
            version: 1
            name: TestName
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV3>
        {
            FilePath = filePath,
            FormatProvider = new YamlFormatProvider()
        };

        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name]
        });
        builder.UseMigration<MySettingsV2, MySettingsV3>(v2 => new MySettingsV3
        {
            Configs = v2.Names.Select(name => new FooConfig { Name = name }).ToArray()
        });

        var options = builder.BuildOptions("");
        var provider = new YamlFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe(3);
        result.Configs.Length.ShouldBe(1);
        result.Configs[0].Name.ShouldBe("TestName");
    }

    [Fact]
    public void LoadConfiguration_Yaml_ShouldApplySingleMigration()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.yaml");
        File.WriteAllText(
            filePath,
            """
            version: 1
            name: TestName
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV2>
        {
            FilePath = filePath,
            FormatProvider = new YamlFormatProvider()
        };

        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name]
        });

        var options = builder.BuildOptions("");
        var provider = new YamlFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe(2);
        result.Names.Length.ShouldBe(1);
        result.Names[0].ShouldBe("TestName");
    }

    // Test model classes
    public class MySettingsV1 : IHasVersion
    {
        public int Version => 1;
        public string Name { get; set; } = "";
    }

    public class MySettingsV2 : IHasVersion
    {
        public int Version => 2;
        public string[] Names { get; set; } = [];
    }

    public class MySettingsV3 : IHasVersion
    {
        public int Version => 3;
        public FooConfig[] Configs { get; set; } = [];
    }

    public class FooConfig
    {
        public string Name { get; set; } = "";
    }
}
