using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

/// <summary>
/// Test configuration class with various properties
/// </summary>
public partial class SourceGenTestConfig
{
    public string Name { get; set; } = "Default Name";
    public int Count { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
    public DateTime Timestamp { get; set; } = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public NestedConfig Nested { get; set; } = new();
}

public partial class NestedConfig
{
    public string Description { get; set; } = "Nested description";
    public double Value { get; set; } = 3.14;
}

/// <summary>
/// JSON Source Generator context for test configuration
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SourceGenTestConfig))]
internal partial class SourceGenTestConfigContext : JsonSerializerContext;

/// <summary>
/// Test configuration with custom naming policy
/// </summary>
public partial class CamelCaseTestConfig
{
    public string FirstName { get; set; } = "John";
    public string LastName { get; set; } = "Doe";
    public int UserAge { get; set; } = 30;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(CamelCaseTestConfig))]
internal partial class CamelCaseTestConfigContext : JsonSerializerContext;

/// <summary>
/// Tests to ensure that JsonFormatProvider works correctly with JSON Source Generators
/// </summary>
public class JsonSourceGeneratorTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Fact]
    public async Task JsonFormatProvider_WithSourceGenerator_ShouldSerializeCorrectly()
    {
        const string testFileName = "sourcegen_test.json";
        var instance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new SourceGenTestConfig
        {
            Name = "Test Name",
            Count = 100,
            IsEnabled = false,
            Timestamp = new DateTime(2025, 12, 19, 10, 30, 0, DateTimeKind.Utc),
            Nested = new NestedConfig { Description = "Test nested", Value = 2.71 },
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
    public async Task JsonFormatProvider_WithSourceGenerator_ShouldDeserializeCorrectly()
    {
        const string testFileName = "sourcegen_load_test.json";
        var instance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // First, save a configuration
        var originalConfig = new SourceGenTestConfig
        {
            Name = "Original Name",
            Count = 999,
            IsEnabled = true,
            Timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Nested = new NestedConfig { Description = "Original nested", Value = 1.41 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Create a new instance to load the saved configuration
        var loadInstance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
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
    public async Task JsonFormatProvider_WithSourceGenerator_AndSectionName_ShouldWork()
    {
        const string testFileName = "sourcegen_section_test.json";
        var instance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "AppSettings:Advanced";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new SourceGenTestConfig { Name = "Section Test", Count = 123 };

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
    public async Task JsonFormatProvider_WithSourceGenerator_SectionName_LoadSaveRoundTrip()
    {
        const string testFileName = "sourcegen_section_roundtrip.json";
        var instance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        // Save initial configuration
        var originalConfig = new SourceGenTestConfig
        {
            Name = "Production DB",
            Count = 999,
            IsEnabled = true,
            Timestamp = new DateTime(2025, 12, 19, 10, 0, 0, DateTimeKind.Utc),
            Nested = new NestedConfig { Description = "Prod settings", Value = 12.34 },
        };

        var option = instance.GetOptions();
        await option.SaveAsync(originalConfig);

        // Verify the saved content has nested structure
        var savedContent = _fileProvider.ReadAllText(testFileName);
        savedContent.ShouldContain("MyApp");
        savedContent.ShouldContain("Settings");
        savedContent.ShouldContain("Database");

        // Create a new instance to load the configuration
        var loadInstance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "MyApp:Settings:Database";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
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
    public async Task JsonFormatProvider_WithSourceGenerator_SectionName_WithUnderscoreSeparator()
    {
        const string testFileName = "sourcegen_section_underscore.json";
        var instance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App__Config__Section";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new SourceGenTestConfig { Name = "Underscore Test", Count = 456 };

        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        // Verify the content is properly nested
        var savedContent = _fileProvider.ReadAllText(testFileName);
        savedContent.ShouldContain("App");
        savedContent.ShouldContain("Config");
        savedContent.ShouldContain("Section");
        savedContent.ShouldContain("Underscore Test");

        // Load and verify
        var loadInstance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();
        loadInstance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App__Config__Section";
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var loadedConfig = loadInstance.GetOptions().CurrentValue;
        loadedConfig.Name.ShouldBe("Underscore Test");
        loadedConfig.Count.ShouldBe(456);
    }

    [Fact]
    public async Task JsonFormatProvider_WithSourceGenerator_RoundTrip_ShouldPreserveData()
    {
        const string testFileName = "sourcegen_roundtrip_test.json";
        var instance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
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
    public async Task JsonFormatProvider_WithSourceGenerator_CamelCase_ShouldWork()
    {
        const string testFileName = "sourcegen_camelcase_test.json";
        var instance = new WritableOptionsSimpleInstance<CamelCaseTestConfig>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = CamelCaseTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var testConfig = new CamelCaseTestConfig
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
    public async Task JsonFormatProvider_SourceGeneratorVsReflection_ShouldProduceSameResult()
    {
        const string sourceGenFileName = "comparison_sourcegen.json";
        const string reflectionFileName = "comparison_reflection.json";

        var testConfig = new SourceGenTestConfig
        {
            Name = "Comparison Test",
            Count = 777,
            IsEnabled = true,
            Timestamp = new DateTime(2025, 3, 20, 15, 45, 30, DateTimeKind.Utc),
            Nested = new NestedConfig { Description = "Comparison nested", Value = 9.99 },
        };

        // Test with Source Generator
        var sourceGenInstance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();
        sourceGenInstance.Initialize(options =>
        {
            options.FilePath = sourceGenFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = SourceGenTestConfigContext.Default,
                },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var sourceGenOption = sourceGenInstance.GetOptions();
        await sourceGenOption.SaveAsync(testConfig);

        // Test with Reflection
        var reflectionInstance = new WritableOptionsSimpleInstance<SourceGenTestConfig>();
        reflectionInstance.Initialize(options =>
        {
            options.FilePath = reflectionFileName;
            options.FormatProvider = new JsonFormatProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true },
            };
            options.UseInMemoryFileProvider(_fileProvider);
        });

        var reflectionOption = reflectionInstance.GetOptions();
        await reflectionOption.SaveAsync(testConfig);

        var sourceGenContent = _fileProvider.ReadAllText(sourceGenFileName);
        var reflectionContent = _fileProvider.ReadAllText(reflectionFileName);

        // Both should produce semantically equivalent JSON
        JsonCompareUtility
            .JsonEquals(sourceGenContent, reflectionContent)
            .ShouldBeTrue(
                "Source Generator and Reflection should produce semantically equivalent JSON output"
            );
    }
}
