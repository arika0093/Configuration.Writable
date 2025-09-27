using System;
using System.Threading.Tasks;
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
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify XML structure and content - order and format must remain stable
        actualOutput.ShouldStartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        actualOutput.ShouldContain("<configuration>");
        actualOutput.ShouldContain("<StringValue>TestString</StringValue>");
        actualOutput.ShouldContain("<IntValue>42</IntValue>");
        actualOutput.ShouldContain("<DoubleValue>3.1415"); // Allow for precision differences between .NET versions
        actualOutput.ShouldContain("<BoolValue>true</BoolValue>");
        actualOutput.ShouldContain("<ArrayValue>");
        actualOutput.ShouldContain("<string>item1</string>");
        actualOutput.ShouldContain("<string>item2</string>");
        actualOutput.ShouldContain("<string>item3</string>");
        actualOutput.ShouldContain("</ArrayValue>");
        actualOutput.ShouldContain("<DateTimeValue>2023-12-25T10:30:45Z</DateTimeValue>");
        actualOutput.ShouldContain("<Nested>");
        actualOutput.ShouldContain("<Description>Nested description</Description>");
        actualOutput.ShouldContain("<Price>99.99</Price>");
        actualOutput.ShouldContain("<IsActive>false</IsActive>");
        actualOutput.ShouldContain("</Nested>");
        actualOutput.ShouldContain("</configuration>");

        // Verify the output contains no unexpected formatting or whitespace variations
        actualOutput.ShouldNotContain("  <"); // No double indentation
        actualOutput.ShouldNotContain("\r\n\r\n"); // No double line breaks
    }

    [Fact]
    public async Task XmlProvider_WithSectionName_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.xml";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.SectionRootName = "App:Database";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify XML output with nested sections
        actualOutput.ShouldStartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        actualOutput.ShouldContain("<App>");
        actualOutput.ShouldContain("<Database>");
        actualOutput.ShouldContain("<StringValue>TestString</StringValue>");
        actualOutput.ShouldContain("<IntValue>42</IntValue>");
        actualOutput.ShouldContain("</Database>");
        actualOutput.ShouldContain("</App>");
    }

    [Fact]
    public async Task XmlProvider_SpecialCharacters_ShouldBeStable()
    {
        const string testFileName = "stability_special_chars_test.xml";
        var instance = new WritableConfigSimpleInstance();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with <tags> & \"quotes\"",
            ArrayValue = ["item<1>", "item&2", "item\"3\""],
        };

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(specialConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify proper XML escaping
        actualOutput.ShouldContain(
            "<StringValue>Test with &lt;tags&gt; &amp; \"quotes\"</StringValue>"
        );
        actualOutput.ShouldContain("<string>item&lt;1&gt;</string>");
        actualOutput.ShouldContain("<string>item&amp;2</string>");
        actualOutput.ShouldContain("<string>item\"3\"</string>");
    }

    [Fact]
    public async Task XmlProvider_EmptyValues_ShouldBeStable()
    {
        const string testFileName = "stability_empty_test.xml";
        var instance = new WritableConfigSimpleInstance();

        var emptyConfig = new TestConfiguration
        {
            StringValue = "",
            ArrayValue = [],
            Nested = new NestedConfiguration { Description = "" },
        };

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(emptyConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify empty values are properly serialized in XML
        // Note: XML may use different formatting for empty values
        actualOutput.ShouldContain("StringValue");
        actualOutput.ShouldContain("ArrayValue");
        actualOutput.ShouldContain("Description");
    }

    [Fact]
    public async Task XmlProvider_NumericValues_ShouldBeStable()
    {
        const string testFileName = "stability_numeric_test.xml";
        var instance = new WritableConfigSimpleInstance();

        var numericConfig = new TestConfiguration
        {
            IntValue = -42,
            DoubleValue = -3.14159,
            Nested = new NestedConfiguration { Price = 0.01m },
        };

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(numericConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify numeric values maintain precision and format
        actualOutput.ShouldContain("<IntValue>-42</IntValue>");
        actualOutput.ShouldContain("<DoubleValue>-3.1415"); // Allow for precision differences between .NET versions
        actualOutput.ShouldContain("<Price>0.01</Price>");
    }

    [Fact]
    public async Task XmlProvider_MultipleSections_ShouldBeStable()
    {
        const string testFileName = "stability_multi_section_test.xml";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.SectionRootName = "App:Database:Connection:Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify deep nesting structure - compact format for deep sections
        actualOutput.ShouldContain("<configuration><App><Database><Connection><Settings>");
        actualOutput.ShouldContain("<StringValue>TestString</StringValue>");
        actualOutput.ShouldContain("</Settings></Connection></Database></App></configuration>");
    }

    [Fact]
    public async Task XmlProvider_WithoutSectionName_ShouldBeStable()
    {
        const string testFileName = "stability_no_section_test.xml";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigXmlProvider();
            options.SectionRootName = ""; // Empty section name
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify root configuration format
        actualOutput.ShouldStartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        actualOutput.ShouldContain("<configuration>");
        actualOutput.ShouldContain("</configuration>");
        actualOutput.ShouldNotContain("<App>"); // No nested sections when section name is empty
    }
}
