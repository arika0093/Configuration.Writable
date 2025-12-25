using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Xml.Tests;

/// <summary>
/// Tests for partial write functionality in XmlFormatProvider.
/// When a SectionName is specified, only that section should be updated while preserving other sections.
/// </summary>
public class XmlPartialWriteTests
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
        const string testFileName = "config.xml";

        // Create initial file with multiple sections
        var initialContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <AppSettings>
                <Name>OldApp</Name>
                <Version>0</Version>
              </AppSettings>
              <UserSettings>
                <Theme>light</Theme>
                <Notifications>false</Notifications>
              </UserSettings>
              <OtherSection>
                <Value>ShouldBePreserved</Value>
              </OtherSection>
            </configuration>
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
            options.FormatProvider = new XmlFormatProvider();
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
        var doc = XDocument.Parse(resultContent);
        var root = doc.Root;

        root.ShouldNotBeNull();

        // Verify AppSettings was updated
        var appSettings = root.Element("AppSettings");
        appSettings.ShouldNotBeNull();
        appSettings.Element("Name")?.Value.ShouldBe("NewApp");
        appSettings.Element("Version")?.Value.ShouldBe("2");

        // Verify UserSettings was preserved
        var userSettings = root.Element("UserSettings");
        userSettings.ShouldNotBeNull();
        userSettings.Element("Theme")?.Value.ShouldBe("light");
        userSettings.Element("Notifications")?.Value.ShouldBe("false");

        // Verify OtherSection was preserved
        var otherSection = root.Element("OtherSection");
        otherSection.ShouldNotBeNull();
        otherSection.Element("Value")?.Value.ShouldBe("ShouldBePreserved");
    }

    [Fact]
    public async Task PartialWrite_WithNestedSection_ShouldUpdateCorrectly()
    {
        // Arrange
        const string testFileName = "nested_config.xml";

        // Create initial file with nested sections
        var initialContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <App>
                <Settings>
                  <Name>OldApp</Name>
                  <Version>0</Version>
                </Settings>
                <Other>
                  <Value>Preserved</Value>
                </Other>
              </App>
            </configuration>
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
            options.FormatProvider = new XmlFormatProvider();
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
        var doc = XDocument.Parse(resultContent);
        var root = doc.Root;

        root.ShouldNotBeNull();

        // Verify nested section was updated
        var app = root.Element("App");
        app.ShouldNotBeNull();
        var settings = app.Element("Settings");
        settings.ShouldNotBeNull();
        settings.Element("Name")?.Value.ShouldBe("UpdatedApp");
        settings.Element("Version")?.Value.ShouldBe("5");

        // Verify sibling section was preserved
        var other = app.Element("Other");
        other.ShouldNotBeNull();
        other.Element("Value")?.Value.ShouldBe("Preserved");
    }

    [Fact]
    public async Task PartialWrite_NoExistingFile_ShouldCreateNewStructure()
    {
        // Arrange
        const string testFileName = "new_config.xml";

        var instance = new WritableOptionsSimpleInstance<AppSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings";
            options.FormatProvider = new XmlFormatProvider();
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
        var doc = XDocument.Parse(resultContent);
        var root = doc.Root;

        root.ShouldNotBeNull();

        // Should create nested structure
        var appSettings = root.Element("AppSettings");
        appSettings.ShouldNotBeNull();
        appSettings.Element("Name")?.Value.ShouldBe("BrandNewApp");
        appSettings.Element("Version")?.Value.ShouldBe("1");
    }

    [Fact]
    public async Task PartialWrite_SectionDoesNotExist_ShouldAddNewSection()
    {
        // Arrange
        const string testFileName = "partial_add.xml";

        var initialContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <ExistingSection>
                <Value>Exists</Value>
              </ExistingSection>
            </configuration>
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
            options.FormatProvider = new XmlFormatProvider();
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
        var doc = XDocument.Parse(resultContent);
        var root = doc.Root;

        root.ShouldNotBeNull();

        // Verify new section was added
        var newSection = root.Element("NewSection");
        newSection.ShouldNotBeNull();
        newSection.Element("Name")?.Value.ShouldBe("AddedApp");
        newSection.Element("Version")?.Value.ShouldBe("3");

        // Verify existing section was preserved
        var existingSection = root.Element("ExistingSection");
        existingSection.ShouldNotBeNull();
        existingSection.Element("Value")?.Value.ShouldBe("Exists");
    }

    [Fact]
    public async Task FullWrite_NoSectionName_ShouldOverwriteEntireFile()
    {
        // Arrange
        const string testFileName = "full_write.xml";

        var initialContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <OldSection>
                <Value>ShouldBeRemoved</Value>
              </OldSection>
            </configuration>
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
            options.FormatProvider = new XmlFormatProvider();
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
        var doc = XDocument.Parse(resultContent);
        var root = doc.Root;

        root.ShouldNotBeNull();

        // Should contain only the new data, no configuration wrapper
        root.Name.LocalName.ShouldBe("AppSettings");
        root.Element("Name")?.Value.ShouldBe("CompletelyNew");
        root.Element("Version")?.Value.ShouldBe("99");
    }

}
