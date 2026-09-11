using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Migration;
using Shouldly;

namespace Configuration.Writable.Tests;

public partial class MigrationSupportTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Test]
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
        result.Configs.Length.ShouldBe(1);
        result.Configs[0].Name.ShouldBe("Test");
    }

    [Test]
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

        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Names.Length.ShouldBe(1);
        result.Names[0].ShouldBe("TestName");
    }

    [Test]
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
        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Configs.Length.ShouldBe(1);
        result.Configs[0].Name.ShouldBe("TestName");
    }

    [Test]
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

    [Test]
    public async Task LoadWithMigration_ShouldDeserializeDirectly_WhenTargetIsUnversioned()
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

    [Test]
    public async Task LoadWithMigration_ShouldNotReadSchemaMetadata_WhenTargetIsUnversioned()
    {
        const string fileName = "settings-unversioned.json";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes("""{"Version":"business","Name":"TestName"}""")
        );

        var builder = new WritableOptionsConfigBuilder<SettingsWithBusinessVersion>
        {
            FilePath = fileName,
            FormatProvider = new JsonFormatProvider(),
            FileProvider = _fileProvider,
        };

        var result = new JsonFormatProvider().LoadWithMigration(builder.BuildOptions(""));

        result.Version.ShouldBe("business");
        result.Name.ShouldBe("TestName");
    }

    // Test model classes
    [OptionsModel(Id = "MigrationSupportSettings", Version = 1)]
    public partial class MySettingsV1
    {
        public string Name { get; set; } = "";
    }

    [OptionsModel(Id = "MigrationSupportSettings", Version = 2)]
    public partial class MySettingsV2
    {
        public string[] Names { get; set; } = [];

        public MySettingsV2 Migrate(MySettingsV1 source) => new() { Names = [source.Name] };
    }

    [OptionsModel(Id = "MigrationSupportSettings", Version = 3)]
    public partial class MySettingsV3
    {
        public FooConfig[] Configs { get; set; } = [];

        public MySettingsV3 Migrate(MySettingsV2 source) =>
            new()
            {
                Configs = source.Names.Select(name => new FooConfig { Name = name }).ToArray(),
            };
    }

    [OptionsModel]
    public partial class FooConfig
    {
        public string Name { get; set; } = "";
    }

    [OptionsModel]
    public partial class SettingsWithoutVersion
    {
        public string Name { get; set; } = "";
    }

    public class SettingsWithBusinessVersion
    {
        public string Version { get; set; } = "";
        public string Name { get; set; } = "";
    }

    [Test]
    public async Task LoadWithMigration_ShouldTreatMissingFileVersionAsVersionOne()
    {
        // Arrange
        var fileName = "settings-none.json";
        var content = """
            {
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
        var options = builder.BuildOptions("");
        var provider = new JsonFormatProvider();

        // Act
        var result = provider.LoadWithMigration(options);

        // Assert
        result.ShouldNotBeNull();
        result.Names.Length.ShouldBe(1);
        result.Names[0].ShouldBe("TestName");
    }
}
