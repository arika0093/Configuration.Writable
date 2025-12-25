using System;
using System.IO;
using System.Linq;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Xunit;

namespace Configuration.Writable.Tests;

public class MigrationSupportTests : IDisposable
{
    private readonly string _tempDirectory;

    public MigrationSupportTests()
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
    public void UseMigration_ShouldThrowException_WhenDowngradeDetected()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV3>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.UseMigration<MySettingsV3, MySettingsV2>(v3 => new MySettingsV2());
        });

        Assert.Contains("downgrade", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version 3", exception.Message);
        Assert.Contains("version 2", exception.Message);
    }

    [Fact]
    public void UseMigration_ShouldThrowException_WhenSameVersionMigration()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV2>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.UseMigration<MySettingsV2, MySettingsV2>(v2 => new MySettingsV2());
        });

        Assert.Contains("downgrade", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UseMigration_ShouldRegisterMigrationStep()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV3>();

        // Act
        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name]
        });

        var options = builder.BuildOptions("");

        // Assert
        Assert.Single(options.MigrationSteps);
        Assert.Equal(typeof(MySettingsV1), options.MigrationSteps[0].FromType);
        Assert.Equal(typeof(MySettingsV2), options.MigrationSteps[0].ToType);
    }

    [Fact]
    public void UseMigration_ShouldRegisterMultipleMigrationSteps()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV3>();

        // Act
        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name]
        });
        builder.UseMigration<MySettingsV2, MySettingsV3>(v2 => new MySettingsV3
        {
            Configs = v2.Names.Select(name => new FooConfig { Name = name }).ToArray()
        });

        var options = builder.BuildOptions("");

        // Assert
        Assert.Equal(2, options.MigrationSteps.Count);
        Assert.Equal(typeof(MySettingsV1), options.MigrationSteps[0].FromType);
        Assert.Equal(typeof(MySettingsV2), options.MigrationSteps[0].ToType);
        Assert.Equal(typeof(MySettingsV2), options.MigrationSteps[1].FromType);
        Assert.Equal(typeof(MySettingsV3), options.MigrationSteps[1].ToType);
    }

    [Fact]
    public void LoadConfiguration_ShouldDeserializeDirectly_WhenVersionMatches()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(
            filePath,
            """
            {
                "Version": 3,
                "Configs": [
                    { "Name": "Test" }
                ]
            }
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV3>
        {
            FilePath = filePath,
            FormatProvider = new JsonFormatProvider()
        };

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Version);
        Assert.Single(result.Configs);
        Assert.Equal("Test", result.Configs[0].Name);
    }

    [Fact]
    public void LoadConfiguration_ShouldApplySingleMigration_WhenVersionIsOlder()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(
            filePath,
            """
            {
                "Version": 1,
                "Name": "TestName"
            }
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV2>
        {
            FilePath = filePath,
            FormatProvider = new JsonFormatProvider()
        };

        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name]
        });

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Version);
        Assert.Single(result.Names);
        Assert.Equal("TestName", result.Names[0]);
    }

    [Fact]
    public void LoadConfiguration_ShouldApplyMultipleMigrations_WhenVersionIsOlder()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(
            filePath,
            """
            {
                "Version": 1,
                "Name": "TestName"
            }
            """
        );

        var builder = new WritableOptionsConfigBuilder<MySettingsV3>
        {
            FilePath = filePath,
            FormatProvider = new JsonFormatProvider()
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
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Version);
        Assert.Single(result.Configs);
        Assert.Equal("TestName", result.Configs[0].Name);
    }

    [Fact]
    public void LoadConfiguration_ShouldDeserializeDirectly_WhenNoMigrationsRegistered()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(
            filePath,
            """
            {
                "Name": "TestName"
            }
            """
        );

        var builder = new WritableOptionsConfigBuilder<SettingsWithoutVersion>
        {
            FilePath = filePath,
            FormatProvider = new JsonFormatProvider()
        };

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
    }

    [Fact]
    public void LoadConfiguration_ShouldDeserializeDirectly_WhenTypeDoesNotImplementIHasVersion()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(
            filePath,
            """
            {
                "Version": 1,
                "Name": "TestName"
            }
            """
        );

        var builder = new WritableOptionsConfigBuilder<SettingsWithoutVersion>
        {
            FilePath = filePath,
            FormatProvider = new JsonFormatProvider()
        };

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadConfiguration(options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
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

    public class SettingsWithoutVersion
    {
        public string Name { get; set; } = "";
    }
}
