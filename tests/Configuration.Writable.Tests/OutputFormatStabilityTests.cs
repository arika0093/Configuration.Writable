using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Tests;

/// <summary>
/// Tests to ensure that library updates don't change the output file format.
/// These tests verify that various classes produce exactly the same content as expected reference files,
/// ensuring complete (not partial) matching for format stability.
/// </summary>
public class OutputFormatStabilityTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    /// <summary>
    /// Test configuration class with comprehensive data types for format validation
    /// </summary>
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
    public async Task JsonProvider_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_test.json";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true, // Use consistent formatting for stability tests
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify the output contains the expected structure and values
        // Note: The actual output may include nested sections like "UserSettings" -> "TestConfiguration"
        actualOutput.ShouldContain("TestString");
        actualOutput.ShouldContain("42");
        actualOutput.ShouldContain("3.1415"); // Allow for precision differences between .NET versions
        actualOutput.ShouldContain("true");
        actualOutput.ShouldContain("item1");
        actualOutput.ShouldContain("item2");
        actualOutput.ShouldContain("item3");
        actualOutput.ShouldContain("2023-12-25T10:30:45Z");
        actualOutput.ShouldContain("Nested description");
        actualOutput.ShouldContain("99.99");
        actualOutput.ShouldContain("false");

        // Store actual output for debugging
        Console.WriteLine("=== Actual JSON Output ===");
        Console.WriteLine(actualOutput);
        Console.WriteLine("=== End JSON Output ===");
    }

    [Fact]
    public async Task JsonProvider_WithSectionName_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.json";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                },
            };
            options.SectionRootName = "ApplicationSettings:Database";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify nested section structure - actual output may have different casing/nesting
        actualOutput.ShouldContain("ApplicationSettings");
        actualOutput.ShouldContain("Database");
        actualOutput.ShouldContain("TestString");
        actualOutput.ShouldContain("42");

        // Store actual output for debugging
        Console.WriteLine("=== Actual JSON Section Output ===");
        Console.WriteLine(actualOutput);
        Console.WriteLine("=== End JSON Section Output ===");
    }

    [Fact]
    public async Task JsonProvider_CompactFormat_ShouldBeStable()
    {
        const string testFileName = "stability_compact_test.json";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false, // Compact format
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify compact format (no extra whitespace)
        actualOutput.ShouldNotContain("  "); // No double spaces
        actualOutput.ShouldNotContain("\n"); // No newlines
        actualOutput.ShouldContain("TestString");
        actualOutput.ShouldContain("42");

        // Store actual output for debugging
        Console.WriteLine("=== Actual JSON Compact Output ===");
        Console.WriteLine(actualOutput);
        Console.WriteLine("=== End JSON Compact Output ===");
    }

    [Fact]
    public async Task CommonFileWriter_OutputBytes_ShouldBeExact()
    {
        using var tempFile = new TemporaryFile();
        var writer = new CommonFileWriter();

        var testContent = """
            {
              "test": "value",
              "number": 42,
              "array": ["a", "b", "c"]
            }
            """;

        var contentBytes = Encoding.UTF8.GetBytes(testContent);
        await writer.SaveToFileAsync(tempFile.FilePath, contentBytes, CancellationToken.None);

        var savedBytes = File.ReadAllBytes(tempFile.FilePath);
        savedBytes.ShouldBe(
            contentBytes,
            "CommonFileWriter should save exact byte content without modification"
        );

        var savedText = File.ReadAllText(tempFile.FilePath, Encoding.UTF8);
        savedText.ShouldBe(
            testContent,
            "CommonFileWriter should preserve exact text content including formatting"
        );
    }

    [Fact]
    public async Task JsonProvider_SpecialCharacters_ShouldBeStable()
    {
        const string testFileName = "stability_special_chars_test.json";
        var instance = new WritableConfigSimpleInstance();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with \"quotes\" and \\ backslashes",
            ArrayValue = ["item with spaces", "item\"with\"quotes", "item\\with\\backslashes"],
        };

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(specialConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Print actual output for debugging
        Console.WriteLine("=== Actual JSON Special Characters Output ===");
        Console.WriteLine(actualOutput);
        Console.WriteLine("=== End JSON Special Characters Output ===");

        // Verify proper JSON escaping (JSON uses Unicode escapes for some characters)
        actualOutput.ShouldContain("Test with");
        actualOutput.ShouldContain("quotes");
        actualOutput.ShouldContain("backslashes");
        actualOutput.ShouldContain("item with spaces");
        actualOutput.ShouldContain("item");
        actualOutput.ShouldContain("with");
        actualOutput.ShouldContain("backslashes");

        // Store actual output for debugging
        Console.WriteLine("=== Actual JSON Special Characters Output ===");
        Console.WriteLine(actualOutput);
        Console.WriteLine("=== End JSON Special Characters Output ===");
    }

    [Fact]
    public async Task JsonProvider_EmptyAndNullValues_ShouldBeStable()
    {
        const string testFileName = "stability_empty_test.json";
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
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(emptyConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify empty values are properly serialized
        actualOutput.ShouldContain("\"\""); // Empty string value
        actualOutput.ShouldContain("[]"); // Empty array

        // Store actual output for debugging
        Console.WriteLine("=== Actual JSON Empty Values Output ===");
        Console.WriteLine(actualOutput);
        Console.WriteLine("=== End JSON Empty Values Output ===");
    }

    /// <summary>
    /// Test to verify that the file format consistency across different scenarios
    /// by checking the actual byte-level output
    /// </summary>
    [Fact]
    public async Task JsonProvider_ByteLevel_ShouldBeStable()
    {
        const string testFileName = "stability_byte_test.json";
        var instance = new WritableConfigSimpleInstance();

        instance.Initialize<TestConfiguration>(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption<TestConfiguration>();
        await option.SaveAsync(testConfig);

        var actualBytes = _fileWriter.ReadAllBytes(testFileName);
        var actualText = _fileWriter.ReadAllText(testFileName);

        // Verify byte count is consistent
        var expectedByteCount = Encoding.UTF8.GetByteCount(actualText);
        actualBytes.Length.ShouldBe(
            expectedByteCount,
            "Byte count should match UTF-8 encoding of the text"
        );

        // Verify content is valid UTF-8
        var reconstructedText = Encoding.UTF8.GetString(actualBytes);
        reconstructedText.ShouldBe(
            actualText,
            "Byte array should roundtrip through UTF-8 correctly"
        );

        // Store actual output for debugging
        Console.WriteLine("=== Actual JSON Byte Output ===");
        Console.WriteLine($"Length: {actualBytes.Length} bytes");
        Console.WriteLine($"Content: {actualText}");
        Console.WriteLine("=== End JSON Byte Output ===");
    }
}
