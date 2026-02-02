using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Configuration.Writable.Yaml.Tests;

/// <summary>
/// Test configuration class for VYaml AOT provider tests
/// </summary>
[YamlObject]
public partial class AotYamlTestConfig
{
    public string Name { get; set; } = "Default Name";
    public int Count { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
    public AotYamlNestedConfig Nested { get; set; } = new();
}

[YamlObject]
public partial class AotYamlNestedConfig
{
    public string Description { get; set; } = "Nested description";
    public double Value { get; set; } = 3.14;
}

/// <summary>
/// Tests for YamlAotFormatProvider using VYaml
/// </summary>
public class YamlAotFormatProviderTests
{
    private readonly InMemoryFileProvider _fileProvider = new();
    private readonly YamlSerializerOptions _options;

    public YamlAotFormatProviderTests()
    {
        // Create default VYaml options with standard resolver
        _options = YamlSerializerOptions.Standard;
    }

    [Fact]
    public void YamlAotFormatProvider_ShouldHaveCorrectFileExtension()
    {
        var provider = new YamlAotFormatProvider(_options);
        provider.FileExtension.ShouldBe("yaml");
    }

    [Fact]
    public void YamlAotFormatProvider_Constructor_WithNullOptions_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => new YamlAotFormatProvider(null!));
    }

    [Fact]
    public async Task YamlAotFormatProvider_ShouldSerializeCorrectly()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotYamlTestConfig
        {
            Name = "Test Name",
            Count = 100,
            IsEnabled = false,
            Nested = new AotYamlNestedConfig { Description = "Test nested", Value = 2.71 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Verify the content contains expected values
        savedContent.ShouldContain("Test Name");
        savedContent.ShouldContain("100");
        savedContent.ShouldContain("false");
        savedContent.ShouldContain("Test nested");
        savedContent.ShouldContain("2.71");
    }

    [Fact]
    public async Task YamlAotFormatProvider_ShouldDeserializeCorrectly()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // First, save a configuration
        var originalConfig = new AotYamlTestConfig
        {
            Name = "Original Name",
            Count = 999,
            IsEnabled = true,
            Nested = new AotYamlNestedConfig { Description = "Original nested", Value = 1.41 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Create a new instance to load the saved configuration
        var loadInstance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Verify all values are correctly loaded
        loadedConfig.Name.ShouldBe("Original Name");
        loadedConfig.Count.ShouldBe(999);
        loadedConfig.IsEnabled.ShouldBe(true);
        loadedConfig.Nested.Description.ShouldBe("Original nested");
        loadedConfig.Nested.Value.ShouldBe(1.41);
    }

    [Fact]
    public async Task YamlAotFormatProvider_WithSectionName_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings:Advanced";
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotYamlTestConfig { Name = "Section Test", Count = 123 };

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
    public async Task YamlAotFormatProvider_SectionName_LoadSaveRoundTrip()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Save initial configuration
        var originalConfig = new AotYamlTestConfig
        {
            Name = "Production DB",
            Count = 999,
            IsEnabled = true,
            Nested = new AotYamlNestedConfig { Description = "Prod settings", Value = 12.34 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Verify the saved content has nested structure
        var savedContent = _fileProvider.ReadAllText(testFileName);
        savedContent.ShouldContain("MyApp");
        savedContent.ShouldContain("Settings");
        savedContent.ShouldContain("Database");

        // Create a new instance to load the configuration
        var loadInstance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Verify all values are correctly loaded from the nested section
        loadedConfig.Name.ShouldBe("Production DB");
        loadedConfig.Count.ShouldBe(999);
        loadedConfig.IsEnabled.ShouldBe(true);
        loadedConfig.Nested.Description.ShouldBe("Prod settings");
        loadedConfig.Nested.Value.ShouldBe(12.34);

        // Modify and save again
        await loadedOption.SaveAsync(config =>
        {
            config.Name = "Production DB Updated";
            config.Count = 1000;
        });

        // Load again to verify the update
        var reloadedConfig = loadedOption.CurrentValue;
        reloadedConfig.Name.ShouldBe("Production DB Updated");
        reloadedConfig.Count.ShouldBe(1000);
        // Other fields should remain unchanged
        reloadedConfig.IsEnabled.ShouldBe(true);
        reloadedConfig.Nested.Description.ShouldBe("Prod settings");
    }

    [Fact]
    public async Task YamlAotFormatProvider_RoundTrip_ShouldPreserveData()
    {
        var testFileName = Path.GetRandomFileName();
        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var option = instance.GetOptions();

        // Save initial configuration
        await option.SaveAsync(config =>
        {
            config.Name = "First Save";
            config.Count = 1;
        });

        var firstLoad = option.CurrentValue;
        firstLoad.Name.ShouldBe("First Save");
        firstLoad.Count.ShouldBe(1);

        // Update and save again
        await option.SaveAsync(config =>
        {
            config.Name = "Second Save";
            config.Count = 2;
        });

        var secondLoad = option.CurrentValue;
        secondLoad.Name.ShouldBe("Second Save");
        secondLoad.Count.ShouldBe(2);

        // Verify the nested object is still intact
        secondLoad.Nested.ShouldNotBeNull();
        secondLoad.Nested.Description.ShouldBe("Nested description");
    }

    [Fact]
    public async Task YamlAotFormatProvider_PartialUpdate_ShouldPreserveOtherSections()
    {
        var testFileName = Path.GetRandomFileName();

        // Pre-populate with multiple sections
        var preContent = @"OtherSection:
  Data: preserved
AppSettings:
  Name: Old";
        var bytes = System.Text.Encoding.UTF8.GetBytes(preContent);
        await _fileProvider.SaveToFileAsync(testFileName, bytes);

        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings";
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(new AotYamlTestConfig { Name = "New Name", Count = 999 });

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Other sections should be preserved
        savedContent.ShouldContain("OtherSection");
        savedContent.ShouldContain("preserved");
        // New data should be written
        savedContent.ShouldContain("New Name");
        savedContent.ShouldContain("999");
    }

    [Fact]
    public async Task YamlAotFormatProvider_SectionWithMissingSection_ShouldReturnDefaultInstance()
    {
        var testFileName = Path.GetRandomFileName();

        // Pre-populate with a different section
        var preContent = @"OtherSection:
  Name: Other";
        var bytes = System.Text.Encoding.UTF8.GetBytes(preContent);
        await _fileProvider.SaveToFileAsync(testFileName, bytes);

        var instance = new WritableOptionsSimpleInstance<AotYamlTestConfig>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "NonExistentSection";
            options.FormatProvider = new YamlAotFormatProvider(_options);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = instance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Should return default instance values since section doesn't exist
        loadedConfig.Name.ShouldBe("Default Name");
        loadedConfig.Count.ShouldBe(42);
        loadedConfig.IsEnabled.ShouldBe(true);
    }
}
