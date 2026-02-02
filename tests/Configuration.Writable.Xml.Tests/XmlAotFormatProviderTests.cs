using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Xml.Tests;

public class XmlAotFormatProviderTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [XmlRoot("TestSettings")]
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
        [XmlArray("Items")]
        [XmlArrayItem("Item")]
        public string[] Items { get; set; } = ["item1", "item2"];
    }

    [XmlRoot("NestedSettings")]
    public class NestedSettings
    {
        public string Description { get; set; } = "nested";
        public ChildSettings Child { get; set; } = new();
    }

    public class ChildSettings
    {
        public int Count { get; set; } = 10;
        public string Status { get; set; } = "active";
    }

    private static Func<Type, XmlSerializer> CreateSerializerFactory()
    {
        return type =>
        {
            if (type == typeof(TestSettings))
                return new XmlSerializer(typeof(TestSettings));
            if (type == typeof(NestedSettings))
                return new XmlSerializer(typeof(NestedSettings));
            return new XmlSerializer(type);
        };
    }

    [Fact]
    public void XmlAotFormatProvider_ShouldHaveCorrectFileExtension()
    {
        var provider = new XmlAotFormatProvider(CreateSerializerFactory());
        provider.FileExtension.ShouldBe("xml");
    }

    [Fact]
    public void XmlAotFormatProvider_Constructor_WithNullFactory_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => new XmlAotFormatProvider(null!));
    }

    [Fact]
    public async Task XmlAotFormatProvider_ShouldSerializeCorrectly()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new TestSettings
        {
            Name = "Test Name",
            Value = 100,
            IsEnabled = false,
            Items = ["test1", "test2", "test3"],
        };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Verify the content contains expected values
        savedContent.ShouldContain("Test Name");
        savedContent.ShouldContain("100");
        savedContent.ShouldContain("false");
        savedContent.ShouldContain("test1");
        savedContent.ShouldContain("test2");
    }

    [Fact]
    public async Task XmlAotFormatProvider_ShouldDeserializeCorrectly()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // First, save a configuration
        var originalConfig = new TestSettings
        {
            Name = "Original Name",
            Value = 999,
            IsEnabled = true,
            Items = ["original1", "original2"],
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Create a new instance to load the saved configuration
        var loadInstance = new WritableOptionsSimpleInstance<TestSettings>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Verify all values are correctly loaded
        loadedConfig.Name.ShouldBe("Original Name");
        loadedConfig.Value.ShouldBe(999);
        loadedConfig.IsEnabled.ShouldBe(true);
        loadedConfig.Items.ShouldBe(new[] { "original1", "original2" });
    }

    [Fact]
    public async Task XmlAotFormatProvider_WithSectionName_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings:Advanced";
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new TestSettings { Name = "Section Test", Value = 123 };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Verify the content is properly nested in the section
        savedContent.ShouldContain("AppSettings");
        savedContent.ShouldContain("Advanced");
        savedContent.ShouldContain("Section Test");
        savedContent.ShouldContain("123");
    }

    [Fact]
    public async Task XmlAotFormatProvider_SectionName_LoadSaveRoundTrip()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Save initial configuration
        var originalConfig = new TestSettings
        {
            Name = "Production DB",
            Value = 999,
            IsEnabled = true,
            Items = ["db1", "db2"],
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Verify the saved content has nested structure
        var savedContent = _fileProvider.ReadAllText(testFileName);
        savedContent.ShouldContain("MyApp");
        savedContent.ShouldContain("Settings");
        savedContent.ShouldContain("Database");

        // Create a new instance to load the configuration
        var loadInstance = new WritableOptionsSimpleInstance<TestSettings>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Verify all values are correctly loaded from the nested section
        loadedConfig.Name.ShouldBe("Production DB");
        loadedConfig.Value.ShouldBe(999);
        loadedConfig.IsEnabled.ShouldBe(true);
        loadedConfig.Items.ShouldBe(new[] { "db1", "db2" });

        // Modify and save again
        await loadedOption.SaveAsync(config =>
        {
            config.Name = "Production DB Updated";
            config.Value = 1000;
        });

        // Load again to verify the update
        var reloadedConfig = loadedOption.CurrentValue;
        reloadedConfig.Name.ShouldBe("Production DB Updated");
        reloadedConfig.Value.ShouldBe(1000);
        // Other fields should remain unchanged
        reloadedConfig.IsEnabled.ShouldBe(true);
    }

    [Fact]
    public async Task XmlAotFormatProvider_RoundTrip_ShouldPreserveData()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var option = instance.GetOptions();

        // Save initial configuration
        await option.SaveAsync(config =>
        {
            config.Name = "First Save";
            config.Value = 1;
        });

        var firstLoad = option.CurrentValue;
        firstLoad.Name.ShouldBe("First Save");
        firstLoad.Value.ShouldBe(1);

        // Update and save again
        await option.SaveAsync(config =>
        {
            config.Name = "Second Save";
            config.Value = 2;
        });

        var secondLoad = option.CurrentValue;
        secondLoad.Name.ShouldBe("Second Save");
        secondLoad.Value.ShouldBe(2);
    }

    [Fact]
    public async Task XmlAotFormatProvider_PartialUpdate_ShouldPreserveOtherSections()
    {
        var testFileName = Path.GetRandomFileName();

        // Pre-populate with multiple sections
        var preContent =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <OtherSection>
    <Data>preserved</Data>
  </OtherSection>
  <AppSettings>
    <Name>Old</Name>
  </AppSettings>
</configuration>";
        var bytes = System.Text.Encoding.UTF8.GetBytes(preContent);
        await _fileProvider.SaveToFileAsync(testFileName, bytes);

        var instance = new WritableOptionsSimpleInstance<TestSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings";
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(new TestSettings { Name = "New Name", Value = 999 });

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Other sections should be preserved
        savedContent.ShouldContain("OtherSection");
        savedContent.ShouldContain("preserved");
        // New data should be written
        savedContent.ShouldContain("New Name");
        savedContent.ShouldContain("999");
    }

    [Fact]
    public async Task XmlAotFormatProvider_WithNestedObjects_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<NestedSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new NestedSettings
        {
            Description = "Test nested",
            Child = new ChildSettings { Count = 42, Status = "testing" },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        // Load and verify
        var loadInstance = new WritableOptionsSimpleInstance<NestedSettings>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new XmlAotFormatProvider(CreateSerializerFactory());
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        loadedConfig.Description.ShouldBe("Test nested");
        loadedConfig.Child.Count.ShouldBe(42);
        loadedConfig.Child.Status.ShouldBe("testing");
    }
}
