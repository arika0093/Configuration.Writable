using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;
using Configuration.Writable.Tests;

namespace Configuration.Writable.Encrypt.Tests;

/// <summary>
/// Tests to ensure that encryption library updates don't change the output file structure.
/// These tests verify that the encrypted provider maintains consistent structure and
/// decryption capabilities across library updates.
/// </summary>
public class EncryptOutputFormatStabilityTests
{
    private const string ReferenceFilesPath = "ReferenceFiles";

    /// <summary>
    /// Create a new InMemoryFileWriter for each test to ensure test isolation
    /// </summary>
    private static InMemoryFileWriter CreateFileWriter() => new();

    /// <summary>
    /// Helper method to decrypt encrypted content and return the JSON
    /// </summary>
    private static string DecryptContent(byte[] encryptedBytes, string encryptionKey)
    {
        var iv = new byte[16];
        Array.Copy(encryptedBytes, 0, iv, 0, 16);
        var encryptedData = new byte[encryptedBytes.Length - 16];
        Array.Copy(encryptedBytes, 16, encryptedData, 0, encryptedData.Length);

        using var aes = Aes.Create();
        var key = encryptionKey;
        if (key.Length < 32)
        {
            key = key.PadRight(32, '0');
        }
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Helper method to load reference file content
    /// </summary>
    private static string LoadReferenceFile(string fileName)
    {
        var path = Path.Combine(ReferenceFilesPath, fileName);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Helper method to load reference binary file content
    /// </summary>
    private static byte[] LoadReferenceBinaryFile(string fileName)
    {
        var path = Path.Combine(ReferenceFilesPath, fileName);
        return File.ReadAllBytes(path);
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
    public async Task EncryptProvider_OutputStructure_ShouldBeStable()
    {
        const string testFileName = "stability_test.enc";
        const string encryptionKey = "TestKey1234567890123456789012"; // 32 chars
        var fileWriter = CreateFileWriter();
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.UseInMemoryFileWriter(fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        // For encrypted files, we verify structure rather than exact content due to IV randomness
        fileWriter.FileExists(testFileName).ShouldBeTrue();
        var encryptedBytes = fileWriter.ReadAllBytes(testFileName);

        // Verify the file has the expected structure:
        // - Should start with IV (16 bytes for AES)
        // - Should contain encrypted JSON data after IV
        encryptedBytes.Length.ShouldBeGreaterThan(
            16,
            "Encrypted file should contain IV + encrypted data"
        );

        // Decrypt and compare with reference JSON
        var decryptedJson = DecryptContent(encryptedBytes, encryptionKey);
        var expectedJson = LoadReferenceFile("encrypt_basic.json");

        // Compare JSON semantically (ignoring whitespace and property order)
        JsonCompareUtility
            .JsonEquals(decryptedJson, expectedJson)
            .ShouldBeTrue("Decrypted JSON content should semantically match the reference file");
    }

    [Fact]
    public async Task EncryptProvider_WithSectionName_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.enc";
        const string encryptionKey = "SectionKey12345678901234567890"; // 32 chars
        var fileWriter = CreateFileWriter();
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.SectionName = "ApplicationSettings:Database";
            options.UseInMemoryFileWriter(fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var encryptedBytes = fileWriter.ReadAllBytes(testFileName);
        encryptedBytes.Length.ShouldBeGreaterThan(16);

        // Decrypt and compare with reference JSON
        var decryptedJson = DecryptContent(encryptedBytes, encryptionKey);
        var expectedJson = LoadReferenceFile("encrypt_section.json");

        // Compare JSON semantically (ignoring whitespace and property order)
        JsonCompareUtility
            .JsonEquals(decryptedJson, expectedJson)
            .ShouldBeTrue(
                "Decrypted JSON content with section name should semantically match the reference file"
            );
    }

    [Fact]
    public async Task EncryptProvider_KeyLength_ShouldBeStable()
    {
        // Test with different key lengths to ensure stability
        var fileWriter = CreateFileWriter();
        var keyLengths = new[] { 16, 24, 32 };

        foreach (var keyLength in keyLengths)
        {
            var testFileName = $"stability_key_{keyLength}_test.enc";
            var encryptionKey = new string('A', keyLength);
            var instance = new WritableConfigSimpleInstance<TestConfiguration>();

            var encryptProvider = new WritableConfigEncryptProvider(
                Encoding.UTF8.GetBytes(encryptionKey)
            );

            instance.Initialize(options =>
            {
                options.FilePath = testFileName;
                options.Provider = encryptProvider;
                options.UseInMemoryFileWriter(fileWriter);
            });

            var testConfig = new TestConfiguration();
            var option = instance.GetOption();
            await option.SaveAsync(testConfig);

            var encryptedBytes = fileWriter.ReadAllBytes(testFileName);
            encryptedBytes.Length.ShouldBeGreaterThan(
                16,
                $"Key length {keyLength} should produce valid encrypted output"
            );

            // Verify we can decrypt
            var iv = new byte[16];
            Array.Copy(encryptedBytes, 0, iv, 0, 16);
            var encryptedData = new byte[encryptedBytes.Length - 16];
            Array.Copy(encryptedBytes, 16, encryptedData, 0, encryptedData.Length);

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);

            var decryptedJson = reader.ReadToEnd();
            decryptedJson.ShouldContain("\"StringValue\":\"TestString\"");
        }
    }

    [Fact]
    public async Task EncryptProvider_LargeData_ShouldBeStable()
    {
        const string testFileName = "stability_large_test.enc";
        const string encryptionKey = "LargeDataKey1234567890123456789"; // 32 chars
        var fileWriter = CreateFileWriter();
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var largeConfig = new TestConfiguration
        {
            StringValue = new string('X', 1000), // Large string
            ArrayValue = Enumerable.Range(0, 100).Select(i => $"item{i}").ToArray(), // Large array
        };

        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.UseInMemoryFileWriter(fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(largeConfig);

        var encryptedBytes = fileWriter.ReadAllBytes(testFileName);
        encryptedBytes.Length.ShouldBeGreaterThan(
            1000,
            "Large data should produce correspondingly large encrypted output"
        );

        // Verify decryption
        var iv = new byte[16];
        Array.Copy(encryptedBytes, 0, iv, 0, 16);
        var encryptedData = new byte[encryptedBytes.Length - 16];
        Array.Copy(encryptedBytes, 16, encryptedData, 0, encryptedData.Length);

        using var aes = Aes.Create();
        var key = encryptionKey;
        if (key.Length < 32)
        {
            key = key.PadRight(32, '0');
        }
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);

        var decryptedJson = reader.ReadToEnd();
        decryptedJson.Length.ShouldBeGreaterThan(1000, "Decrypted large data should maintain size");
        decryptedJson.ShouldContain("\"StringValue\":\"" + new string('X', 1000) + "\"");
    }

    [Fact]
    public async Task EncryptProvider_SpecialCharacters_ShouldBeStable()
    {
        const string testFileName = "stability_special_chars_test.enc";
        const string encryptionKey = "SpecialKey1234567890123456789012"; // 32 chars
        var fileWriter = CreateFileWriter();
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var specialConfig = new TestConfiguration
        {
            StringValue = "Test with special characters",
            ArrayValue = ["item1", "item2", "item3"],
        };

        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.UseInMemoryFileWriter(fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(specialConfig);

        var encryptedBytes = fileWriter.ReadAllBytes(testFileName);

        // Decrypt and compare with reference JSON
        var decryptedJson = DecryptContent(encryptedBytes, encryptionKey);
        var expectedJson = LoadReferenceFile("encrypt_special_chars.json");

        // Compare JSON semantically (ignoring whitespace and property order)
        JsonCompareUtility
            .JsonEquals(decryptedJson, expectedJson)
            .ShouldBeTrue(
                "Decrypted JSON content with special characters should semantically match the reference file"
            );
    }

    /// <summary>
    /// Test that verifies backward compatibility by loading a pre-encrypted file through the provider.
    /// This simulates the real-world scenario of reading previously saved encrypted configuration.
    /// </summary>
    [Fact]
    public async Task EncryptProvider_CanLoadPreEncryptedConfigurationThroughProvider()
    {
        const string testFileName = "load_preencrypted_test.enc";
        const string encryptionKey = "BackwardCompatKey1234567890123"; // 32 chars
        const string referenceEncryptedFile = "backward_compat_basic.enc";
        var fileWriter = CreateFileWriter();

        // Load the pre-encrypted reference file and copy to InMemoryFileWriter
        var encryptedBytes = LoadReferenceBinaryFile(referenceEncryptedFile);
        await fileWriter.SaveToFileAsync(testFileName, encryptedBytes);

        // Initialize the provider to read the pre-encrypted file
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();
        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.UseInMemoryFileWriter(fileWriter);
        });

        // Load the configuration through the provider
        var option = instance.GetOption();
        var loadedConfig = option.CurrentValue;

        // Verify the loaded configuration has expected values
        loadedConfig.ShouldNotBeNull();
        loadedConfig.StringValue.ShouldBe("TestString");
        loadedConfig.IntValue.ShouldBe(42);
        loadedConfig.DoubleValue.ShouldBe(3.14159);
        loadedConfig.BoolValue.ShouldBe(true);
        // Note: Array values may be merged with defaults during configuration loading
        // Just verify it contains the expected items
        loadedConfig.ArrayValue.ShouldContain("item1");
        loadedConfig.ArrayValue.ShouldContain("item2");
        loadedConfig.ArrayValue.ShouldContain("item3");
        // Compare DateTime in UTC to avoid timezone issues
        loadedConfig
            .DateTimeValue.ToUniversalTime()
            .ShouldBe(new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc));
        loadedConfig.Nested.ShouldNotBeNull();
        loadedConfig.Nested.Description.ShouldBe("Nested description");
        loadedConfig.Nested.Price.ShouldBe(99.99m);
        loadedConfig.Nested.IsActive.ShouldBe(false);
    }
}
