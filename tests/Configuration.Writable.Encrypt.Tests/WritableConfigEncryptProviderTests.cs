using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Encrypt.Tests;

public class WritableConfigEncryptProviderTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
        public string SecretKey { get; set; } = "secret123";
    }

    [Fact]
    public void WritableConfigEncryptProvider_WithStringKey_ShouldInitialize()
    {
        var provider = new WritableConfigEncryptProvider("myencryptionkey123456789");
        provider.ShouldNotBeNull();
        provider.Key.Length.ShouldBe(32);
    }

    [Fact]
    public void WritableConfigEncryptProvider_WithShortKey_ShouldPadKey()
    {
        var provider = new WritableConfigEncryptProvider("short");
        provider.Key.Length.ShouldBe(32);

        var keyString = Encoding.UTF8.GetString(provider.Key);
        keyString.ShouldStartWith("short");
        keyString.ShouldEndWith("000000000000000000000000000");
    }

    [Fact]
    public void WritableConfigEncryptProvider_WithByteKey_ShouldUseKey()
    {
        var key = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            key[i] = (byte)(i % 256);
        }

        var provider = new WritableConfigEncryptProvider(key);
        provider.Key.ShouldBe(key);
    }

    [Fact]
    public void WritableConfigEncryptProvider_WithInvalidKeyLength_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => new WritableConfigEncryptProvider(new byte[10]));
        Should.Throw<ArgumentException>(() => new WritableConfigEncryptProvider(new byte[33]));
    }

    [Fact]
    public void WritableConfigEncryptProvider_WithNullKey_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => new WritableConfigEncryptProvider((string)null!));
        Should.Throw<ArgumentNullException>(() => new WritableConfigEncryptProvider(""));
    }

    [Fact]
    public async Task Initialize_WithEncryptProvider_ShouldCreateEncryptedFile()
    {
        var testFileName = Path.GetRandomFileName();
        var encryptionKey = "myencryptionkey123456789012345";

        var _instance = new WritableConfigSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigEncryptProvider(encryptionKey);
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var settings = new TestSettings
        {
            Name = "encrypted_test",
            Value = 999,
            IsEnabled = false,
            SecretKey = "topsecret",
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(settings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileBytes = _fileWriter.ReadAllBytes(testFileName);
        fileBytes.Length.ShouldBeGreaterThan(0);

        var fileText = Encoding.UTF8.GetString(fileBytes);
        fileText.ShouldNotContain("encrypted_test");
        fileText.ShouldNotContain("topsecret");
        fileText.ShouldNotContain("999");
    }

    [Fact]
    public async Task LoadAndSave_WithEncryptProvider_ShouldPreserveData()
    {
        var testFileName = Path.GetRandomFileName();
        var encryptionKey = "myencryptionkey123456789012345";
        var provider = new WritableConfigEncryptProvider(encryptionKey);
        provider.FileWriter = _fileWriter;

        var _instance = new WritableConfigSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = provider;
        });

        var originalSettings = new TestSettings
        {
            Name = "encrypt_persistence_test",
            Value = 777,
            IsEnabled = true,
            SecretKey = "supersecret",
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(originalSettings);

        // Debug: Verify file was created and has content
        _fileWriter.FileExists(testFileName).ShouldBeTrue();
        var fileBytes = _fileWriter.ReadAllBytes(testFileName);
        fileBytes.Length.ShouldBeGreaterThan(16); // Should have at least IV (16 bytes) + some encrypted content

        // Re-initialize with the same provider to simulate reloading
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = provider;
        });

        option = _instance.GetOptions();
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("encrypt_persistence_test");
        loadedSettings.Value.ShouldBe(777);
        loadedSettings.IsEnabled.ShouldBeTrue();
        loadedSettings.SecretKey.ShouldBe("supersecret");
    }

    [Fact]
    public async Task LoadWithDifferentKey_ShouldHandleDecryptionFailure()
    {
        var testFileName = Path.GetRandomFileName();
        var encryptionKey1 = "myencryptionkey123456789012345";
        var encryptionKey2 = "differentkey12345678901234567";

        var provider1 = new WritableConfigEncryptProvider(encryptionKey1);
        provider1.FileWriter = _fileWriter;

        var _instance = new WritableConfigSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = provider1;
        });

        var originalSettings = new TestSettings
        {
            Name = "encrypt_key_test",
            Value = 555,
            SecretKey = "keytest",
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(originalSettings);

        // When loading with a different key, it should fail to decrypt and return default values
        var provider2 = new WritableConfigEncryptProvider(encryptionKey2);
        provider2.FileWriter = _fileWriter;

        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = provider2;
        });

        option = _instance.GetOptions();
        var loadedSettings = option.CurrentValue;

        // Since decryption failed, should return default values
        loadedSettings.Name.ShouldBe("default");
        loadedSettings.Value.ShouldBe(42);
        loadedSettings.SecretKey.ShouldBe("secret123");
    }

    [Fact]
    public async Task SaveAsync_WithEncryptProvider_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();
        var encryptionKey = "asyncencryptionkey12345678901";

        var _instance = new WritableConfigSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigEncryptProvider(encryptionKey);
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = _instance.GetOptions();
        await option.SaveAsync(settings =>
        {
            settings.Name = "async_encrypt_test";
            settings.Value = 666;
            settings.SecretKey = "asyncsecret";
        });

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("async_encrypt_test");
        loadedSettings.Value.ShouldBe(666);
        loadedSettings.SecretKey.ShouldBe("asyncsecret");
    }
}
