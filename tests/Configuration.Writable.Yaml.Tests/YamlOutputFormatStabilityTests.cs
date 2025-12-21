using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Yaml.Tests;

/// <summary>
/// Tests to ensure that YAML library updates don't change the output file format.
/// These tests verify that YAML provider produces exactly the same content as expected,
/// ensuring complete (not partial) matching for format stability.
/// </summary>
public class YamlOutputFormatStabilityTests
{
    private readonly InMemoryFileProvider _FileProvider = new();
    private const string ReferenceFilesPath = "ReferenceFiles";

    /// <summary>
    /// Helper method to normalize YAML for comparison (normalizes line endings)
    /// YAML is whitespace-sensitive, so we only normalize line endings
    /// </summary>
    private static string NormalizeYaml(string yaml)
    {
        // Only normalize line endings - YAML is whitespace-sensitive
        return yaml.Replace("\r\n", "\n").TrimEnd();
    }

    /// <summary>
    /// Helper method to load reference file content
    /// </summary>
    private static string LoadReferenceFile(string fileName)
    {
        var path = Path.Combine(ReferenceFilesPath, fileName);
        return File.ReadAllText(path);
    }

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
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var actualOutput = _FileProvider.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("yaml_basic.yaml");

        // Compare normalized YAML (normalize line endings only)
        var actualNormalized = NormalizeYaml(actualOutput);
        var expectedNormalized = NormalizeYaml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "YAML output format should exactly match the reference file"
        );
    }

    [Fact]
    public async Task YamlProvider_WithSectionName_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.yaml";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.SectionName = "app:settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var actualOutput = _FileProvider.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("yaml_section.yaml");

        // Compare normalized YAML
        var actualNormalized = NormalizeYaml(actualOutput);
        var expectedNormalized = NormalizeYaml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "YAML output with section name should exactly match the reference file"
        );
    }

    [Fact]
    public async Task YamlProvider_SpecialCharacters_ShouldBeStable()
    {
        const string testFileName = "stability_special_chars_test.yaml";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with \"quotes\" and: colons",
            ArrayValue = ["item with spaces", "item:with:colons", "item\"with\"quotes"],
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(specialConfig);

        var actualOutput = _FileProvider.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("yaml_special_chars.yaml");

        // Compare normalized YAML
        var actualNormalized = NormalizeYaml(actualOutput);
        var expectedNormalized = NormalizeYaml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "YAML output with special characters should exactly match the reference file"
        );
    }

    [Fact]
    public async Task YamlProvider_EmptyValues_ShouldBeStable()
    {
        const string testFileName = "stability_empty_test.yaml";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        var emptyConfig = new TestConfiguration
        {
            StringValue = "",
            ArrayValue = [],
            Nested = new NestedConfiguration { Description = "" },
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(emptyConfig);

        var actualOutput = _FileProvider.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("yaml_empty.yaml");

        // Compare normalized YAML
        var actualNormalized = NormalizeYaml(actualOutput);
        var expectedNormalized = NormalizeYaml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "YAML output with empty values should exactly match the reference file"
        );
    }

    [Fact]
    public async Task YamlProvider_NumericValues_ShouldBeStable()
    {
        const string testFileName = "stability_numeric_test.yaml";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        var numericConfig = new TestConfiguration
        {
            IntValue = -42,
            DoubleValue = -3.14159,
            Nested = new NestedConfiguration { Price = 0.01m },
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(numericConfig);

        var actualOutput = _FileProvider.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("yaml_numeric.yaml");

        // Compare normalized YAML
        var actualNormalized = NormalizeYaml(actualOutput);
        var expectedNormalized = NormalizeYaml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "YAML output with numeric values should exactly match the reference file"
        );
    }

    [Fact]
    public async Task YamlProvider_MultipleSections_ShouldBeStable()
    {
        const string testFileName = "stability_multi_section_test.yaml";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.SectionName = "app:database:connection:settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var actualOutput = _FileProvider.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("yaml_multi_section.yaml");

        // Compare normalized YAML
        var actualNormalized = NormalizeYaml(actualOutput);
        var expectedNormalized = NormalizeYaml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "YAML output with multiple sections should exactly match the reference file"
        );
    }
}
