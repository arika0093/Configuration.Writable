using System;
using System.IO;
using System.Linq;
using Configuration.Writable.Configure;
using Configuration.Writable.FormatProvider;
using Shouldly;
using Xunit;

namespace Configuration.Writable.Xml.Tests;

public class XmlMigrationSupportTests : IDisposable
{
    private readonly string _tempDirectory;

    public XmlMigrationSupportTests()
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
    public void LoadConfiguration_Xml_ShouldApplyMultipleMigrations()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.xml");
        File.WriteAllText(
            filePath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <Version>1</Version>
                <Name>TestName</Name>
            </configuration>
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV3>
        {
            FilePath = filePath,
            FormatProvider = new XmlFormatProvider()
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
        var provider = new XmlFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe(3);
        result.Configs.Length.ShouldBe(1);
        result.Configs[0].Name.ShouldBe("TestName");
    }

    [Fact]
    public void LoadConfiguration_Xml_ShouldApplySingleMigration()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.xml");
        File.WriteAllText(
            filePath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <Version>1</Version>
                <Name>TestName</Name>
            </configuration>
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV2>
        {
            FilePath = filePath,
            FormatProvider = new XmlFormatProvider()
        };

        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name]
        });

        var options = builder.BuildOptions("");
        var provider = new XmlFormatProvider();

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
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
    }

    public class MySettingsV2 : IHasVersion
    {
        public int Version { get; set; } = 2;
        public string[] Names { get; set; } = [];
    }

    public class MySettingsV3 : IHasVersion
    {
        public int Version { get; set; } = 3;
        public FooConfig[] Configs { get; set; } = [];
    }

    public class FooConfig
    {
        public string Name { get; set; } = "";
    }
}
