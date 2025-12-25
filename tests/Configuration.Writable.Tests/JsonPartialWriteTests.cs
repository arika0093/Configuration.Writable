using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Tests;

/// <summary>
/// Tests for partial write functionality in JsonFormatProvider.
/// When a SectionName is specified, only that section should be updated while preserving other sections.
/// </summary>
public class JsonPartialWriteTests
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
        const string testFileName = "appsettings.json";

        // Create initial file with multiple sections
        var initialContent = """
            {
              "AppSettings": {
                "Name": "OldApp",
                "Version": 0
              },
              "UserSettings": {
                "Theme": "light",
                "Notifications": false
              },
              "OtherSection": {
                "Value": "ShouldBePreserved"
              }
            }
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
            options.SectionName = "AppSettings";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
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
        using var doc = JsonDocument.Parse(resultContent);
        var root = doc.RootElement;

        // Verify AppSettings was updated
        root.GetProperty("AppSettings").GetProperty("Name").GetString().ShouldBe("NewApp");
        root.GetProperty("AppSettings").GetProperty("Version").GetInt32().ShouldBe(2);

        // Verify UserSettings was preserved
        root.GetProperty("UserSettings").GetProperty("Theme").GetString().ShouldBe("light");
        root.GetProperty("UserSettings")
            .GetProperty("Notifications")
            .GetBoolean()
            .ShouldBe(false);

        // Verify OtherSection was preserved
        root.GetProperty("OtherSection")
            .GetProperty("Value")
            .GetString()
            .ShouldBe("ShouldBePreserved");
    }

    [Fact]
    public async Task PartialWrite_WithNestedSection_ShouldUpdateCorrectly()
    {
        // Arrange
        const string testFileName = "config.json";

        // Create initial file with nested sections
        var initialContent = """
            {
              "App": {
                "Settings": {
                  "Name": "OldApp",
                  "Version": 0
                },
                "Other": {
                  "Value": "Preserved"
                }
              }
            }
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
            options.SectionName = "App:Settings";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
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
        using var doc = JsonDocument.Parse(resultContent);
        var root = doc.RootElement;

        // Verify nested section was updated
        var appSettings = root.GetProperty("App").GetProperty("Settings");
        appSettings.GetProperty("Name").GetString().ShouldBe("UpdatedApp");
        appSettings.GetProperty("Version").GetInt32().ShouldBe(5);

        // Verify sibling section was preserved
        root.GetProperty("App")
            .GetProperty("Other")
            .GetProperty("Value")
            .GetString()
            .ShouldBe("Preserved");
    }

    [Fact]
    public async Task PartialWrite_NoExistingFile_ShouldCreateNewStructure()
    {
        // Arrange
        const string testFileName = "new_config.json";

        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
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
        using var doc = JsonDocument.Parse(resultContent);
        var root = doc.RootElement;

        // Should create nested structure
        root.GetProperty("AppSettings").GetProperty("Name").GetString().ShouldBe("BrandNewApp");
        root.GetProperty("AppSettings").GetProperty("Version").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task PartialWrite_SectionDoesNotExist_ShouldAddNewSection()
    {
        // Arrange
        const string testFileName = "partial_add.json";

        var initialContent = """
            {
              "ExistingSection": {
                "Value": "Exists"
              }
            }
            """;
        await _fileProvider.SaveToFileAsync(
            testFileName,
            Encoding.UTF8.GetBytes(initialContent)
        );

        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "NewSection";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
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
        using var doc = JsonDocument.Parse(resultContent);
        var root = doc.RootElement;

        // Verify new section was added
        root.GetProperty("NewSection").GetProperty("Name").GetString().ShouldBe("AddedApp");
        root.GetProperty("NewSection").GetProperty("Version").GetInt32().ShouldBe(3);

        // Verify existing section was preserved
        root.GetProperty("ExistingSection")
            .GetProperty("Value")
            .GetString()
            .ShouldBe("Exists");
    }

    [Fact]
    public async Task FullWrite_NoSectionName_ShouldOverwriteEntireFile()
    {
        // Arrange
        const string testFileName = "full_write.json";

        var initialContent = """
            {
              "OldSection": {
                "Value": "ShouldBeRemoved"
              }
            }
            """;
        await _fileProvider.SaveToFileAsync(
            testFileName,
            Encoding.UTF8.GetBytes(initialContent)
        );

        // Initialize without SectionName
        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            // No SectionName specified - full overwrite
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
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
        using var doc = JsonDocument.Parse(resultContent);
        var root = doc.RootElement;

        // Should contain only the new data, no nested structure
        root.GetProperty("Name").GetString().ShouldBe("CompletelyNew");
        root.GetProperty("Version").GetInt32().ShouldBe(99);

        // Old section should not exist
        root.TryGetProperty("OldSection", out _).ShouldBe(false);
    }

}
