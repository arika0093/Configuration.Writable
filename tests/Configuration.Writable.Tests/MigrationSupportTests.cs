using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Migration;
using Shouldly;
using Xunit;

namespace Configuration.Writable.Tests;

public class MigrationSupportTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Fact]
    public void UseMigration_ShouldThrowException_WhenDowngradeDetected()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV3>();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            builder.UseMigration<MySettingsV3, MySettingsV2>(v3 => new MySettingsV2());
        });

        exception.Message.ShouldContain("downgrade", Case.Insensitive);
        exception.Message.ShouldContain("version 3");
        exception.Message.ShouldContain("version 2");
    }

    [Fact]
    public void UseMigration_ShouldThrowException_WhenSameVersionMigration()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV2>();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            builder.UseMigration<MySettingsV2, MySettingsV2>(v2 => new MySettingsV2());
        });

        exception.Message.ShouldContain("downgrade", Case.Insensitive);
    }

    [Fact]
    public void UseMigration_ShouldRegisterMigrationStep()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV3>();

        // Act
        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name],
        });

        var options = builder.BuildOptions("");

        // Assert
        options.MigrationSteps.Count.ShouldBe(1);
        options.MigrationSteps[0].FromType.ShouldBe(typeof(MySettingsV1));
        options.MigrationSteps[0].ToType.ShouldBe(typeof(MySettingsV2));
    }

    [Fact]
    public void UseMigration_ShouldRegisterMultipleMigrationSteps()
    {
        // Arrange
        var builder = new WritableOptionsConfigBuilder<MySettingsV3>();

        // Act
        builder.Logger = ConsoleLoggerFactory.Create();
        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name],
        });
        builder.UseMigration<MySettingsV2, MySettingsV3>(v2 => new MySettingsV3
        {
            Configs = v2.Names.Select(name => new FooConfig { Name = name }).ToArray(),
        });

        var options = builder.BuildOptions("");

        // Assert
        options.MigrationSteps.Count.ShouldBe(2);
        options.MigrationSteps[0].FromType.ShouldBe(typeof(MySettingsV1));
        options.MigrationSteps[0].ToType.ShouldBe(typeof(MySettingsV2));
        options.MigrationSteps[1].FromType.ShouldBe(typeof(MySettingsV2));
        options.MigrationSteps[1].ToType.ShouldBe(typeof(MySettingsV3));
    }

    [Fact]
    public async Task LoadWithMigration_ShouldDeserializeDirectly_WhenVersionMatches()
    {
        // Arrange
        var fileName = "settings1.json";
        var content = """
            {
                "Version": 3,
                "Configs": [
                    { "Name": "Test" }
                ]
            }
            """;
        await _fileProvider.SaveToFileAsync(fileName, Encoding.UTF8.GetBytes(content));

        var builder = new WritableOptionsConfigBuilder<MySettingsV3>
        {
            FilePath = fileName,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.FileProvider = _fileProvider;

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe(3);
        result.Configs.Length.ShouldBe(1);
        result.Configs[0].Name.ShouldBe("Test");
    }

    [Fact]
    public async Task LoadWithMigration_ShouldApplySingleMigration_WhenVersionIsOlder()
    {
        // Arrange
        var fileName = "settings2.json";
        var content = """
            {
                "Version": 1,
                "Name": "TestName"
            }
            """;
        await _fileProvider.SaveToFileAsync(fileName, Encoding.UTF8.GetBytes(content));

        var builder = new WritableOptionsConfigBuilder<MySettingsV2>
        {
            FilePath = fileName,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.FileProvider = _fileProvider;

        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name],
        });

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe(2);
        result.Names.Length.ShouldBe(1);
        result.Names[0].ShouldBe("TestName");
    }

    [Fact]
    public async Task LoadWithMigration_ShouldApplyMultipleMigrations_WhenVersionIsOlder()
    {
        // Arrange
        var fileName = "settings3.json";
        var content = """
            {
                "Version": 1,
                "Name": "TestName"
            }
            """;
        await _fileProvider.SaveToFileAsync(fileName, Encoding.UTF8.GetBytes(content));

        var builder = new WritableOptionsConfigBuilder<MySettingsV3>
        {
            FilePath = fileName,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.FileProvider = _fileProvider;
        builder.UseMigration<MySettingsV1, MySettingsV2>(v1 => new MySettingsV2
        {
            Names = [v1.Name],
        });
        builder.UseMigration<MySettingsV2, MySettingsV3>(v2 => new MySettingsV3
        {
            Configs = v2.Names.Select(name => new FooConfig { Name = name }).ToArray(),
        });

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe(3);
        result.Configs.Length.ShouldBe(1);
        result.Configs[0].Name.ShouldBe("TestName");
    }

    [Fact]
    public async Task LoadWithMigration_ShouldDeserializeDirectly_WhenNoMigrationsRegistered()
    {
        // Arrange
        var fileName = "settings4.json";
        var content = """
            {
                "Name": "TestName"
            }
            """;
        await _fileProvider.SaveToFileAsync(fileName, Encoding.UTF8.GetBytes(content));

        var builder = new WritableOptionsConfigBuilder<SettingsWithoutVersion>
        {
            FilePath = fileName,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.FileProvider = _fileProvider;
        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("TestName");
    }

    [Fact]
    public async Task LoadWithMigration_ShouldDeserializeDirectly_WhenTypeDoesNotImplementIHasVersion()
    {
        // Arrange
        var fileName = "settings5.json";
        var content = """
            {
                "Version": 1,
                "Name": "TestName"
            }
            """;
        await _fileProvider.SaveToFileAsync(fileName, Encoding.UTF8.GetBytes(content));

        var builder = new WritableOptionsConfigBuilder<SettingsWithoutVersion>
        {
            FilePath = fileName,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.FileProvider = _fileProvider;
        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("TestName");
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

    public class SettingsWithoutVersion
    {
        public string Name { get; set; } = "";
    }
}
