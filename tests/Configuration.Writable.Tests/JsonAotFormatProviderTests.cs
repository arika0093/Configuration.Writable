using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Tests;

/// <summary>
/// Test configuration class for AOT provider tests
/// </summary>
public partial class AotTestConfig
{
    public string Name { get; set; } = "Default Name";
    public int Count { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
    public DateTime Timestamp { get; set; } = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public AotNestedConfig Nested { get; set; } = new();
}

public partial class AotNestedConfig
{
    public string Description { get; set; } = "Nested description";
    public double Value { get; set; } = 3.14;
}

/// <summary>
/// JSON Source Generator context for AOT test configuration
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AotTestConfig))]
[JsonSerializable(typeof(AotNestedConfig))]
internal partial class AotTestConfigContext : JsonSerializerContext;

/// <summary>
/// Test configuration with camelCase naming policy
/// </summary>
public partial class AotCamelCaseConfig
{
    public string FirstName { get; set; } = "John";
    public string LastName { get; set; } = "Doe";
    public int UserAge { get; set; } = 30;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(AotCamelCaseConfig))]
internal partial class AotCamelCaseConfigContext : JsonSerializerContext;

/// <summary>
/// Tests to ensure that JsonAotFormatProvider works correctly with JSON Source Generators
/// for AOT-compatible scenarios.
/// </summary>
public class JsonAotFormatProviderTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Fact]
    public async Task JsonAotFormatProvider_ShouldSerializeCorrectly()
    {
        const string testFileName = "aot_serialize_test.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotTestConfig
        {
            Name = "Test Name",
            Count = 100,
            IsEnabled = false,
            Timestamp = new DateTime(2025, 12, 19, 10, 30, 0, DateTimeKind.Utc),
            Nested = new AotNestedConfig { Description = "Test nested", Value = 2.71 },
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
    public async Task JsonAotFormatProvider_ShouldDeserializeCorrectly()
    {
        const string testFileName = "aot_deserialize_test.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // First, save a configuration
        var originalConfig = new AotTestConfig
        {
            Name = "Original Name",
            Count = 999,
            IsEnabled = true,
            Timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Nested = new AotNestedConfig { Description = "Original nested", Value = 1.41 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Create a new instance to load the saved configuration
        var loadInstance = new WritableOptionsSimpleInstance<AotTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Verify all values are correctly loaded
        loadedConfig.Name.ShouldBe("Original Name");
        loadedConfig.Count.ShouldBe(999);
        loadedConfig.IsEnabled.ShouldBe(true);
        loadedConfig.Timestamp.ShouldBe(new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc));
        loadedConfig.Nested.Description.ShouldBe("Original nested");
        loadedConfig.Nested.Value.ShouldBe(1.41);
    }

    [Fact]
    public async Task JsonAotFormatProvider_WithSectionName_ShouldWork()
    {
        const string testFileName = "aot_section_test.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings:Advanced";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotTestConfig { Name = "Section Test", Count = 123 };

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
    public async Task JsonAotFormatProvider_SectionName_LoadSaveRoundTrip()
    {
        const string testFileName = "aot_section_roundtrip.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Save initial configuration
        var originalConfig = new AotTestConfig
        {
            Name = "Production DB",
            Count = 999,
            IsEnabled = true,
            Timestamp = new DateTime(2025, 12, 19, 10, 0, 0, DateTimeKind.Utc),
            Nested = new AotNestedConfig { Description = "Prod settings", Value = 12.34 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Verify the saved content has nested structure
        var savedContent = _fileProvider.ReadAllText(testFileName);
        savedContent.ShouldContain("MyApp");
        savedContent.ShouldContain("Settings");
        savedContent.ShouldContain("Database");

        // Create a new instance to load the configuration
        var loadInstance = new WritableOptionsSimpleInstance<AotTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = loadInstance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Verify all values are correctly loaded from the nested section
        loadedConfig.Name.ShouldBe("Production DB");
        loadedConfig.Count.ShouldBe(999);
        loadedConfig.IsEnabled.ShouldBe(true);
        loadedConfig.Timestamp.ShouldBe(new DateTime(2025, 12, 19, 10, 0, 0, DateTimeKind.Utc));
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
    public async Task JsonAotFormatProvider_SectionName_WithUnderscoreSeparator()
    {
        const string testFileName = "aot_section_underscore.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App__Config__Section";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotTestConfig { Name = "Underscore Test", Count = 456 };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        // Verify the content is properly nested
        var savedContent = _fileProvider.ReadAllText(testFileName);
        savedContent.ShouldContain("App");
        savedContent.ShouldContain("Config");
        savedContent.ShouldContain("Section");
        savedContent.ShouldContain("Underscore Test");

        // Load and verify
        var loadInstance = new WritableOptionsSimpleInstance<AotTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App__Config__Section";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedConfig = loadInstance.GetOptions().CurrentValue;
        loadedConfig.Name.ShouldBe("Underscore Test");
        loadedConfig.Count.ShouldBe(456);
    }

    [Fact]
    public async Task JsonAotFormatProvider_RoundTrip_ShouldPreserveData()
    {
        const string testFileName = "aot_roundtrip_test.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
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
    public async Task JsonAotFormatProvider_WithCamelCaseNamingPolicy_ShouldWork()
    {
        const string testFileName = "aot_camelcase_test.json";
        var instance = new WritableOptionsSimpleInstance<AotCamelCaseConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotCamelCaseConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotCamelCaseConfig
        {
            FirstName = "Jane",
            LastName = "Smith",
            UserAge = 25,
        };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Verify camelCase naming
        savedContent.ShouldContain("firstName");
        savedContent.ShouldContain("lastName");
        savedContent.ShouldContain("userAge");
        savedContent.ShouldContain("Jane");
        savedContent.ShouldContain("Smith");
        savedContent.ShouldContain("25");
    }

    [Fact]
    public async Task JsonAotFormatProvider_VsJsonFormatProvider_ShouldProduceSameResult()
    {
        const string aotFileName = "aot_comparison.json";
        const string reflectionFileName = "reflection_comparison.json";

        var testConfig = new AotTestConfig
        {
            Name = "Comparison Test",
            Count = 777,
            IsEnabled = true,
            Timestamp = new DateTime(2025, 3, 20, 15, 45, 30, DateTimeKind.Utc),
            Nested = new AotNestedConfig { Description = "Comparison nested", Value = 9.99 },
        };

        // Test with AOT provider
        var aotInstance = new WritableOptionsSimpleInstance<AotTestConfig>();
        aotInstance.Initialize(options =>
        {
            options.FilePath = aotFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var aotOption = aotInstance.GetOptions();
        await aotOption.SaveAsync(testConfig);

        // Test with regular JsonFormatProvider (using source generator options)
        var reflectionInstance = new WritableOptionsSimpleInstance<AotTestConfig>();
        reflectionInstance.Initialize(options =>
        {
            options.FilePath = reflectionFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = AotTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var reflectionOption = reflectionInstance.GetOptions();
        await reflectionOption.SaveAsync(testConfig);

        var aotContent = _fileProvider.ReadAllText(aotFileName);
        var reflectionContent = _fileProvider.ReadAllText(reflectionFileName);

        // Both should produce semantically equivalent JSON
        JsonCompareUtility
            .JsonEquals(aotContent, reflectionContent)
            .ShouldBeTrue(
                "JsonAotFormatProvider and JsonFormatProvider should produce semantically equivalent JSON output"
            );
    }

    [Fact]
    public void JsonAotFormatProvider_Constructor_WithNullResolver_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => new JsonAotFormatProvider(null!));
    }

    [Fact]
    public async Task JsonAotFormatProvider_WithCustomJsonSerializerOptions_ShouldOverrideContextOptions()
    {
        const string testFileName = "aot_custom_options.json";
        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();

        var customOptions = new JsonSerializerOptions
        {
            WriteIndented = false, // Override the context's WriteIndented = true
            TypeInfoResolver = AotTestConfigContext.Default,
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default)
            {
                JsonSerializerOptions = customOptions,
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new AotTestConfig { Name = "Custom Options Test", Count = 42 };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Content should not be indented because we overrode the options
        savedContent.ShouldNotContain("\n  "); // Indented JSON would have newlines followed by spaces
        savedContent.ShouldContain("Custom Options Test");
    }

    [Fact]
    public async Task JsonAotFormatProvider_SectionWithMissingSection_ShouldReturnDefaultInstance()
    {
        const string testFileName = "aot_missing_section.json";

        // Pre-populate with a different section
        var preContent = @"{""OtherSection"":{""Name"":""Other""}}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(preContent);
        await _fileProvider.SaveToFileAsync(testFileName, bytes);

        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "NonExistentSection";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedOption = instance.GetOptions();
        var loadedConfig = loadedOption.CurrentValue;

        // Should return default instance values since section doesn't exist
        loadedConfig.Name.ShouldBe("Default Name");
        loadedConfig.Count.ShouldBe(42);
        loadedConfig.IsEnabled.ShouldBe(true);
    }

    [Fact]
    public async Task JsonAotFormatProvider_PartialUpdate_ShouldPreserveOtherSections()
    {
        const string testFileName = "aot_partial_preserve.json";

        // Pre-populate with multiple sections
        var preContent = @"{""OtherSection"":{""Data"":""preserved""},""AppSettings"":{""Name"":""Old""}}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(preContent);
        await _fileProvider.SaveToFileAsync(testFileName, bytes);

        var instance = new WritableOptionsSimpleInstance<AotTestConfig>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings";
            options.FormatProvider = new JsonAotFormatProvider(AotTestConfigContext.Default);
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(new AotTestConfig { Name = "New Name", Count = 999 });

        var savedContent = _fileProvider.ReadAllText(testFileName);

        // Other sections should be preserved
        savedContent.ShouldContain("OtherSection");
        savedContent.ShouldContain("preserved");
        // New data should be written
        savedContent.ShouldContain("New Name");
        savedContent.ShouldContain("999");
    }

    [Fact]
    public void JsonAotFormatProvider_FileExtension_ShouldBeJson()
    {
        var provider = new JsonAotFormatProvider(AotTestConfigContext.Default);
        provider.FileExtension.ShouldBe("json");
    }
}
