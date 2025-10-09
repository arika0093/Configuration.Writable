using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Xml.Tests;

/// <summary>
/// Tests to ensure that XML library updates don't change the output file format.
/// These tests verify that XML provider produces exactly the same content as expected,
/// ensuring complete (not partial) matching for format stability.
/// </summary>
public class XmlOutputFormatStabilityTests
{
    private readonly InMemoryFileWriter _fileWriter = new();
    private const string ReferenceFilesPath = "ReferenceFiles";

    /// <summary>
    /// Helper method to normalize XML for comparison (ignores whitespace/formatting differences)
    /// </summary>
    private static string NormalizeXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.ToString(SaveOptions.DisableFormatting);
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
    public async Task XmlProvider_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_test.xml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_basic.xml");

        // Compare normalized XML (to handle potential whitespace differences)
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output format should exactly match the reference file"
        );
    }

    [Fact]
    public async Task XmlProvider_WithSectionName_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.xml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.SectionName = "App:Database";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_section.xml");

        // Compare normalized XML
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output with section name should exactly match the reference file"
        );
    }

    [Fact]
    public async Task XmlProvider_SpecialCharacters_ShouldBeStable()
    {
        const string testFileName = "stability_special_chars_test.xml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with <tags> & \"quotes\"",
            ArrayValue = ["item<1>", "item&2", "item\"3\""],
        };

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(specialConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_special_chars.xml");

        // Compare normalized XML
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output with special characters should exactly match the reference file"
        );
    }

    [Fact]
    public async Task XmlProvider_EmptyValues_ShouldBeStable()
    {
        const string testFileName = "stability_empty_test.xml";
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
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(emptyConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_empty.xml");

        // Compare normalized XML
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output with empty values should exactly match the reference file"
        );
    }

    [Fact]
    public async Task XmlProvider_NumericValues_ShouldBeStable()
    {
        const string testFileName = "stability_numeric_test.xml";
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
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(numericConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_numeric.xml");

        // Compare normalized XML
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output with numeric values should exactly match the reference file"
        );
    }

    [Fact]
    public async Task XmlProvider_MultipleSections_ShouldBeStable()
    {
        const string testFileName = "stability_multi_section_test.xml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.SectionName = "App:Database:Connection:Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_multi_section.xml");

        // Compare normalized XML
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output with multiple sections should exactly match the reference file"
        );
    }

    [Fact]
    public async Task XmlProvider_WithoutSectionName_ShouldBeStable()
    {
        const string testFileName = "stability_no_section_test.xml";
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.SectionName = ""; // Empty section name
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("xml_no_section.xml");

        // Compare normalized XML
        var actualNormalized = NormalizeXml(actualOutput);
        var expectedNormalized = NormalizeXml(expectedOutput);

        actualNormalized.ShouldBe(
            expectedNormalized,
            "XML output without section name should exactly match the reference file"
        );
    }
}
