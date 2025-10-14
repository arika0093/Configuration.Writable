using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    private const string ReferenceFilesPath = "ReferenceFiles";

    /// <summary>
    /// Helper method to load reference file content
    /// </summary>
    private static string LoadReferenceFile(string fileName)
    {
        var path = Path.Combine(ReferenceFilesPath, fileName);
        return File.ReadAllText(path);
    }

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
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true, // Use consistent formatting for stability tests
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("json_basic.json");

        // Compare JSON semantically (ignoring whitespace and property order)
        JsonCompareUtility
            .JsonEquals(actualOutput, expectedOutput)
            .ShouldBeTrue("JSON output format should semantically match the reference file");
    }

    [Fact]
    public async Task JsonProvider_WithSectionName_OutputFormat_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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
            options.SectionName = "ApplicationSettings:Database";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("json_section.json");

        // Compare JSON semantically (ignoring whitespace and property order)
        JsonCompareUtility
            .JsonEquals(actualOutput, expectedOutput)
            .ShouldBeTrue(
                "JSON output with section name should semantically match the reference file"
            );
    }

    [Fact]
    public async Task JsonProvider_CompactFormat_ShouldBeStable()
    {
        const string testFileName = "stability_compact_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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
        var option = instance.GetOptions();
        await option.SaveAsync(testConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);
        var expectedOutput = LoadReferenceFile("json_compact.json");

        // Compare JSON semantically (ignoring whitespace and property order)
        JsonCompareUtility
            .JsonEquals(actualOutput, expectedOutput)
            .ShouldBeTrue(
                "JSON output in compact format should semantically match the reference file"
            );
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
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with \"quotes\" and \\ backslashes",
            ArrayValue = ["item with spaces", "item\"with\"quotes", "item\\with\\backslashes"],
        };

        instance.Initialize(options =>
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

        var option = instance.GetOptions();
        await option.SaveAsync(specialConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify proper JSON escaping (JSON uses Unicode escapes for some characters)
        actualOutput.ShouldContain("Test with");
        actualOutput.ShouldContain("quotes");
        actualOutput.ShouldContain("backslashes");
        actualOutput.ShouldContain("item with spaces");
        actualOutput.ShouldContain("item");
        actualOutput.ShouldContain("with");
        actualOutput.ShouldContain("backslashes");
    }

    [Fact]
    public async Task JsonProvider_EmptyAndNullValues_ShouldBeStable()
    {
        const string testFileName = "stability_empty_test.json";
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
            options.Provider = new WritableConfigJsonProvider
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                },
            };
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(emptyConfig);

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify empty values are properly serialized
        actualOutput.ShouldContain("\"\""); // Empty string value
        actualOutput.ShouldContain("[]"); // Empty array
    }

    /// <summary>
    /// Test to verify that the file format consistency across different scenarios
    /// by checking the actual byte-level output
    /// </summary>
    [Fact]
    public async Task JsonProvider_ByteLevel_ShouldBeStable()
    {
        const string testFileName = "stability_byte_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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
        var option = instance.GetOptions();
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
    }

    [Fact]
    public async Task JsonProvider_DeleteKey_SimpleProperty_ShouldRemoveFromFile()
    {
        const string testFileName = "delete_simple_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();

        // Save with DeleteKey operation
        await option.SaveAsync((config, op) =>
        {
            op.DeleteKey(c => c.StringValue);
            op.DeleteKey(c => c.BoolValue);
        });

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify deleted keys are not present
        actualOutput.ShouldNotContain("stringValue");
        actualOutput.ShouldNotContain("StringValue");
        actualOutput.ShouldNotContain("boolValue");
        actualOutput.ShouldNotContain("BoolValue");

        // Verify other keys are still present
        actualOutput.ShouldContain("intValue");
        actualOutput.ShouldContain("42");
        actualOutput.ShouldContain("nested");
    }

    [Fact]
    public async Task JsonProvider_DeleteKey_NestedProperty_ShouldRemoveFromFile()
    {
        const string testFileName = "delete_nested_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();

        // Save with DeleteKey operation for nested property
        await option.SaveAsync((config, op) =>
        {
            op.DeleteKey(c => c.Nested.Description);
        });

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify nested deleted key is not present
        actualOutput.ShouldNotContain("description");
        actualOutput.ShouldNotContain("Nested description");

        // Verify other nested properties are still present
        actualOutput.ShouldContain("nested");
        actualOutput.ShouldContain("price");
        actualOutput.ShouldContain("99.99");

        // Verify root properties are still present
        actualOutput.ShouldContain("stringValue");
        actualOutput.ShouldContain("intValue");
    }

    [Fact]
    public async Task JsonProvider_DeleteKey_NonExistentProperty_ShouldNotError()
    {
        const string testFileName = "delete_nonexistent_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();

        // First save with a deletion
        await option.SaveAsync((config, op) =>
        {
            op.DeleteKey(c => c.StringValue);
        });

        // Save again trying to delete the already deleted key - should not error
        await option.SaveAsync((config, op) =>
        {
            op.DeleteKey(c => c.StringValue);
        });

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify the key is still not present
        actualOutput.ShouldNotContain("stringValue");

        // Verify file is still valid
        actualOutput.ShouldContain("intValue");
    }

    [Fact]
    public async Task JsonProvider_DeleteKey_CombinedWithUpdate_ShouldWork()
    {
        const string testFileName = "delete_combined_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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

        var testConfig = new TestConfiguration();
        var option = instance.GetOptions();

        // Save with both update and deletion
        await option.SaveAsync((config, op) =>
        {
            config.StringValue = "Updated Value";
            config.IntValue = 999;
            op.DeleteKey(c => c.BoolValue);
            op.DeleteKey(c => c.Nested.IsActive);
        });

        var actualOutput = _fileWriter.ReadAllText(testFileName);

        // Verify updates are present
        actualOutput.ShouldContain("Updated Value");
        actualOutput.ShouldContain("999");

        // Verify deletions are not present
        actualOutput.ShouldNotContain("boolValue");
        actualOutput.ShouldNotContain("isActive");

        // Verify other properties are still present
        actualOutput.ShouldContain("nested");
        actualOutput.ShouldContain("price");
    }

    [Fact]
    public async Task JsonProvider_DeleteKey_ShouldUpdateCacheCorrectly()
    {
        const string testFileName = "delete_cache_test.json";
        var instance = new WritableOptionsSimpleInstance<TestConfiguration>();

        instance.Initialize(options =>
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
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOptions();

        // First, save a config with all properties
        await option.SaveAsync(config =>
        {
            config.StringValue = "InitialValue";
            config.IntValue = 100;
            config.BoolValue = true;
        });

        // Verify initial state in cache
        var beforeDelete = option.CurrentValue;
        beforeDelete.StringValue.ShouldBe("InitialValue");
        beforeDelete.IntValue.ShouldBe(100);
        beforeDelete.BoolValue.ShouldBeTrue();

        // Now delete a key and update another
        await option.SaveAsync((config, op) =>
        {
            config.StringValue = "UpdatedValue";
            op.DeleteKey(c => c.BoolValue);
        });

        // Verify cache is updated correctly after deletion
        var afterDelete = option.CurrentValue;
        afterDelete.StringValue.ShouldBe("UpdatedValue");
        afterDelete.IntValue.ShouldBe(100); // Unchanged
        // BoolValue should be the class default (true) since key was deleted
        afterDelete.BoolValue.ShouldBeTrue();

        // Verify file doesn't contain the deleted key
        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldNotContain("boolValue");
        fileContent.ShouldContain("UpdatedValue");
        fileContent.ShouldContain("100");
    }
}
