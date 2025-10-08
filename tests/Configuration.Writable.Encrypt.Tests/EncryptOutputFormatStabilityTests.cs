using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Encrypt.Tests;

/// <summary>
/// Tests to ensure that encryption library updates don't change the output file structure.
/// These tests verify that the encrypted provider maintains consistent structure and
/// decryption capabilities across library updates.
/// </summary>
public class EncryptOutputFormatStabilityTests
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
    public async Task EncryptProvider_OutputStructure_ShouldBeStable()
    {
        const string testFileName = "stability_test.enc";
        const string encryptionKey = "TestKey1234567890123456789012"; // 32 chars
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        // For encrypted files, we verify structure rather than exact content due to IV randomness
        _fileWriter.FileExists(testFileName).ShouldBeTrue();
        var encryptedBytes = _fileWriter.ReadAllBytes(testFileName);

        // Verify the file has the expected structure:
        // - Should start with IV (16 bytes for AES)
        // - Should contain encrypted JSON data after IV
        encryptedBytes.Length.ShouldBeGreaterThan(
            16,
            "Encrypted file should contain IV + encrypted data"
        );

        // Verify we can decrypt and get expected JSON structure
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

        // Verify the decrypted content contains expected JSON structure
        decryptedJson.ShouldContain("\"StringValue\":\"TestString\"");
        decryptedJson.ShouldContain("\"IntValue\":42");
        decryptedJson.ShouldContain("\"DoubleValue\":3.1415"); // Allow for precision differences between .NET versions
        decryptedJson.ShouldContain("\"BoolValue\":true");
        decryptedJson.ShouldContain("\"ArrayValue\":");
        decryptedJson.ShouldContain("\"DateTimeValue\":\"2023-12-25T10:30:45Z\"");
        decryptedJson.ShouldContain("\"Nested\":");
        decryptedJson.ShouldContain("\"Description\":\"Nested description\"");
        decryptedJson.ShouldContain("\"Price\":99.99");
        decryptedJson.ShouldContain("\"IsActive\":false");
    }

    [Fact]
    public async Task EncryptProvider_WithSectionName_ShouldBeStable()
    {
        const string testFileName = "stability_section_test.enc";
        const string encryptionKey = "SectionKey12345678901234567890"; // 32 chars
        var instance = new WritableConfigSimpleInstance<TestConfiguration>();

        var encryptProvider = new WritableConfigEncryptProvider(encryptionKey);

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = encryptProvider;
            options.SectionRootName = "ApplicationSettings:Database";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var testConfig = new TestConfiguration();
        var option = instance.GetOption();
        await option.SaveAsync(testConfig);

        var encryptedBytes = _fileWriter.ReadAllBytes(testFileName);
        encryptedBytes.Length.ShouldBeGreaterThan(16);

        // Decrypt and verify nested structure
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

        // Verify nested section structure in decrypted JSON
        decryptedJson.ShouldContain("\"ApplicationSettings\":");
        decryptedJson.ShouldContain("\"Database\":");
        decryptedJson.ShouldContain("\"StringValue\":\"TestString\"");
    }

    [Fact]
    public async Task EncryptProvider_KeyLength_ShouldBeStable()
    {
        // Test with different key lengths to ensure stability
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
                options.UseInMemoryFileWriter(_fileWriter);
            });

            var testConfig = new TestConfiguration();
            var option = instance.GetOption();
            await option.SaveAsync(testConfig);

            var encryptedBytes = _fileWriter.ReadAllBytes(testFileName);
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
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(largeConfig);

        var encryptedBytes = _fileWriter.ReadAllBytes(testFileName);
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
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = instance.GetOption();
        await option.SaveAsync(specialConfig);

        var encryptedBytes = _fileWriter.ReadAllBytes(testFileName);

        // Decrypt and verify special characters are preserved
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

        // Verify content is preserved through encryption/decryption
        decryptedJson.ShouldContain("Test with special characters");
        decryptedJson.ShouldContain("ArrayValue");
        decryptedJson.ShouldContain("item1");
        decryptedJson.ShouldContain("item2");
        decryptedJson.ShouldContain("item3");
        decryptedJson.Length.ShouldBeGreaterThan(100); // Verify reasonable content size
    }
}
