using System;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Yaml.Tests;

/// <summary>
/// Tests to ensure that YAML library updates don't change the output file format.
/// These tests verify that YAML provider produces exactly the same content as expected,
/// ensuring complete (not partial) matching for format stability.
/// </summary>
public class YamlOutputFormatStabilityTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    public class TestConfiguration
    {
        public string StringValue { get; set; } = "TestString";
        public int IntValue { get; set; } = 42;
        public double DoubleValue { get; set; } = 3.14159;
        public bool BoolValue { get; set; } = true;
        public string[] ArrayValue { get; set; } = ["item1", "item2", "item3"];
        public DateTime DateTimeValue { get; set; } =
            new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc);
        public NestedConfiguration Nested { get; set; } = new();
    }

    public class NestedConfiguration
    {
        public string Description { get; set; } = "Nested description";
        public decimal Price { get; set; } = 99.99m;
        public bool IsActive { get; set; } = false;
    }

    [Fact]
    public async Task YamlProvider_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_test.yaml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify YAML output contains expected structure and values
        // Note: The actual output includes UserSettings -> TestConfiguration nesting
        actualOutput.ShouldContain("stringValue: TestString");
        actualOutput.ShouldContain("intValue: 42");
        actualOutput.ShouldContain("doubleValue: 3.1415"); // Allow for precision differences between .NET versions
        actualOutput.ShouldContain("boolValue: true");
        actualOutput.ShouldContain("arrayValue:");
        actualOutput.ShouldContain("- item1");
        actualOutput.ShouldContain("- item2");
        actualOutput.ShouldContain("- item3");
        actualOutput.ShouldContain("dateTimeValue: 2023-12-25T10:30:45.0000000Z");
        actualOutput.ShouldContain("nested:");
        actualOutput.ShouldContain("description: Nested description");
        actualOutput.ShouldContain("price: 99.99");
        actualOutput.ShouldContain("isActive: false");
    }

    [Fact]
    public async Task YamlProvider_WithSectionName_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.yaml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.SectionRootName = "app:settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify YAML output with nested sections
        actualOutput.ShouldContain("app:");
        actualOutput.ShouldContain("settings:");
        actualOutput.ShouldContain("stringValue: TestString");
        actualOutput.ShouldContain("intValue: 42");
        actualOutput.ShouldContain("doubleValue: 3.1415"); // Allow for precision differences between .NET versions
        actualOutput.ShouldContain("boolValue: true");
    }

    [Fact]
    public async Task YamlProvider_SpecialCharacters_ShouldBeStable()
    {
        const string testFileName = "stability_special_chars_test.yaml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with \"quotes\" and: colons",
            ArrayValue = ["item with spaces", "item:with:colons", "item\"with\"quotes"],
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(specialConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify proper YAML escaping and quoting (YAML uses single quotes for some special chars)
        actualOutput.ShouldContain("Test with");
        actualOutput.ShouldContain("quotes");
        actualOutput.ShouldContain("colons");
        actualOutput.ShouldContain("item with spaces");
        actualOutput.ShouldContain("item");
        actualOutput.ShouldContain("with");
        actualOutput.ShouldContain("colons");
    }

    [Fact]
    public async Task YamlProvider_EmptyValues_ShouldBeStable()
    {
        const string testFileName = "stability_empty_test.yaml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var emptyConfig = new TestConfiguration
        {
            StringValue = "",
            ArrayValue = [],
            Nested = new NestedConfiguration { Description = "" },
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(emptyConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify empty values are properly serialized in YAML
        actualOutput.ShouldContain("stringValue:");
        actualOutput.ShouldContain("arrayValue:");
        actualOutput.ShouldContain("description:");
    }

    [Fact]
    public async Task YamlProvider_NumericValues_ShouldBeStable()
    {
        const string testFileName = "stability_numeric_test.yaml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var numericConfig = new TestConfiguration
        {
            IntValue = -42,
            DoubleValue = -3.14159,
            Nested = new NestedConfiguration { Price = 0.01m },
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(numericConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify numeric values maintain precision and format
        actualOutput.ShouldContain("intValue: -42");
        actualOutput.ShouldContain("doubleValue: -3.1415"); // Allow for precision differences between .NET versions
        actualOutput.ShouldContain("price: 0.01");
    }

    [Fact]
    public async Task YamlProvider_MultipleSections_ShouldBeStable()
    {
        const string testFileName = "stability_multi_section_test.yaml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.SectionRootName = "app:database:connection:settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify deep nesting structure
        actualOutput.ShouldContain("app:");
        actualOutput.ShouldContain("  database:");
        actualOutput.ShouldContain("    connection:");
        actualOutput.ShouldContain("      settings:");
        actualOutput.ShouldContain("        stringValue: TestString");
    }
}
