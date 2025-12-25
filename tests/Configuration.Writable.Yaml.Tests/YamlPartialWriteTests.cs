using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Configuration.Writable.Yaml.Tests;

/// <summary>
/// Tests for partial write functionality in YamlFormatProvider.
/// When a SectionName is specified, only that section should be updated while preserving other sections.
/// </summary>
public class YamlPartialWriteTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    public class AppSettings
    {
        public string Name { get; set; } = "MyApp";
        public int Version { get; set; } = 1;
    }

    public class UserSettings
    {
        public string Theme { get; set; } = "dark";
        public bool Notifications { get; set; } = true;
    }

    [Fact]
    public async Task PartialWrite_WithExistingFile_ShouldPreserveOtherSections()
    {
        // Arrange
        const string testFileName = "config.yaml";

        // Create initial file with multiple sections
        var initialContent = """
            appSettings:
              name: OldApp
              version: 0
            userSettings:
              theme: light
              notifications: false
            otherSection:
              value: ShouldBePreserved
            """;
        await _fileProvider.SaveToFileAsync(
            testFileName,
            Encoding.UTF8.GetBytes(initialContent)
        );

        // Initialize writable options for AppSettings section only
        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "appSettings";
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Act - Update only AppSettings section
        var appOptions = instance.GetOptions();
        await appOptions.SaveAsync(setting =>
        {
            setting.Name = "NewApp";
            setting.Version = 2;
        });

        // Assert
        var resultContent = _fileProvider.ReadAllText(testFileName);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var result = deserializer.Deserialize<Dictionary<string, object>>(resultContent);

        result.ShouldNotBeNull();

        // Verify appSettings was updated
        var appSettings = result["appSettings"] as Dictionary<object, object>;
        appSettings.ShouldNotBeNull();
        appSettings["name"].ShouldBe("NewApp");
        appSettings["version"].ToString().ShouldBe("2");

        // Verify userSettings was preserved
        var userSettings = result["userSettings"] as Dictionary<object, object>;
        userSettings.ShouldNotBeNull();
        userSettings["theme"].ShouldBe("light");
        userSettings["notifications"].ToString().ShouldBe("false");

        // Verify otherSection was preserved
        var otherSection = result["otherSection"] as Dictionary<object, object>;
        otherSection.ShouldNotBeNull();
        otherSection["value"].ShouldBe("ShouldBePreserved");
    }

    [Fact]
    public async Task PartialWrite_WithNestedSection_ShouldUpdateCorrectly()
    {
        // Arrange
        const string testFileName = "nested_config.yaml";

        // Create initial file with nested sections
        var initialContent = """
            app:
              settings:
                name: OldApp
                version: 0
              other:
                value: Preserved
            """;
        await _fileProvider.SaveToFileAsync(
            testFileName,
            Encoding.UTF8.GetBytes(initialContent)
        );

        // Initialize writable options for nested section
        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "app:settings";
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Act
        var appOptions = instance.GetOptions();
        await appOptions.SaveAsync(setting =>
        {
            setting.Name = "UpdatedApp";
            setting.Version = 5;
        });

        // Assert
        var resultContent = _fileProvider.ReadAllText(testFileName);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var result = deserializer.Deserialize<Dictionary<string, object>>(resultContent);

        result.ShouldNotBeNull();

        // Verify nested section was updated
        var app = result["app"] as Dictionary<object, object>;
        app.ShouldNotBeNull();
        var settings = app["settings"] as Dictionary<object, object>;
        settings.ShouldNotBeNull();
        settings["name"].ShouldBe("UpdatedApp");
        settings["version"].ToString().ShouldBe("5");

        // Verify sibling section was preserved
        var other = app["other"] as Dictionary<object, object>;
        other.ShouldNotBeNull();
        other["value"].ShouldBe("Preserved");
    }

    [Fact]
    public async Task PartialWrite_NoExistingFile_ShouldCreateNewStructure()
    {
        // Arrange
        const string testFileName = "new_config.yaml";

        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "appSettings";
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Act
        var appOptions = instance.GetOptions();
        await appOptions.SaveAsync(setting =>
        {
            setting.Name = "BrandNewApp";
            setting.Version = 1;
        });

        // Assert
        var resultContent = _fileProvider.ReadAllText(testFileName);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var result = deserializer.Deserialize<Dictionary<string, object>>(resultContent);

        result.ShouldNotBeNull();

        // Should create nested structure
        var appSettings = result["appSettings"] as Dictionary<object, object>;
        appSettings.ShouldNotBeNull();
        appSettings["name"].ShouldBe("BrandNewApp");
        appSettings["version"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task PartialWrite_SectionDoesNotExist_ShouldAddNewSection()
    {
        // Arrange
        const string testFileName = "partial_add.yaml";

        var initialContent = """
            existingSection:
              value: Exists
            """;
        await _fileProvider.SaveToFileAsync(
            testFileName,
            Encoding.UTF8.GetBytes(initialContent)
        );

        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "newSection";
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Act
        var appOptions = instance.GetOptions();
        await appOptions.SaveAsync(setting =>
        {
            setting.Name = "AddedApp";
            setting.Version = 3;
        });

        // Assert
        var resultContent = _fileProvider.ReadAllText(testFileName);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var result = deserializer.Deserialize<Dictionary<string, object>>(resultContent);

        result.ShouldNotBeNull();

        // Verify new section was added
        var newSection = result["newSection"] as Dictionary<object, object>;
        newSection.ShouldNotBeNull();
        newSection["name"].ShouldBe("AddedApp");
        newSection["version"].ToString().ShouldBe("3");

        // Verify existing section was preserved
        var existingSection = result["existingSection"] as Dictionary<object, object>;
        existingSection.ShouldNotBeNull();
        existingSection["value"].ShouldBe("Exists");
    }

    [Fact]
    public async Task FullWrite_NoSectionName_ShouldWriteDirectly()
    {
        // Arrange - No existing file
        const string testFileName = "full_write.yaml";

        // Initialize without SectionName
        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            // No SectionName specified - full overwrite
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Act
        var appOptions = instance.GetOptions();
        await appOptions.SaveAsync(setting =>
        {
            setting.Name = "CompletelyNew";
            setting.Version = 99;
        });

        // Assert
        var resultContent = _fileProvider.ReadAllText(testFileName);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var result = deserializer.Deserialize<Dictionary<string, object>>(resultContent);

        result.ShouldNotBeNull();

        // Should contain only the new data, no nested structure
        result["name"].ShouldBe("CompletelyNew");
        result["version"].ToString().ShouldBe("99");
    }

}
