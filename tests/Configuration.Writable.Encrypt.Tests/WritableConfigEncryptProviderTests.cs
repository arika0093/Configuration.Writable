using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable;

namespace Configuration.Writable.Encrypt.Tests;

public class WritableConfigEncryptProviderTests
{
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
    public void Initialize_WithEncryptProvider_ShouldCreateEncryptedFile()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        var encryptionKey = "myencryptionkey123456789012345";

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = testFileName;
                options.Provider = new WritableConfigEncryptProvider(encryptionKey);
            });

            var settings = new TestSettings
            {
                Name = "encrypted_test",
                Value = 999,
                IsEnabled = false,
                SecretKey = "topsecret"
            };

            WritableConfig.Save(settings);

            File.Exists(testFileName).ShouldBeTrue();

            var fileBytes = File.ReadAllBytes(testFileName);
            fileBytes.Length.ShouldBeGreaterThan(0);

            var fileText = Encoding.UTF8.GetString(fileBytes);
            fileText.ShouldNotContain("encrypted_test");
            fileText.ShouldNotContain("topsecret");
            fileText.ShouldNotContain("999");
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }

    [Fact]
    public void LoadAndSave_WithEncryptProvider_ShouldPreserveData()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        var encryptionKey = "myencryptionkey123456789012345";

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = testFileName;
                options.Provider = new WritableConfigEncryptProvider(encryptionKey);
            });

            var originalSettings = new TestSettings
            {
                Name = "encrypt_persistence_test",
                Value = 777,
                IsEnabled = true,
                SecretKey = "supersecret"
            };

            WritableConfig.Save(originalSettings);

            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = testFileName;
                options.Provider = new WritableConfigEncryptProvider(encryptionKey);
            });

            var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
            loadedSettings.Name.ShouldBe("encrypt_persistence_test");
            loadedSettings.Value.ShouldBe(777);
            loadedSettings.IsEnabled.ShouldBeTrue();
            loadedSettings.SecretKey.ShouldBe("supersecret");
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }

    [Fact]
    public void LoadWithDifferentKey_ShouldHandleDecryptionFailure()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        var encryptionKey1 = "myencryptionkey123456789012345";
        var encryptionKey2 = "differentkey12345678901234567";

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = testFileName;
                options.Provider = new WritableConfigEncryptProvider(encryptionKey1);
            });

            var originalSettings = new TestSettings
            {
                Name = "encrypt_key_test",
                Value = 555,
                SecretKey = "keytest"
            };

            WritableConfig.Save(originalSettings);

            // When loading with a different key, it should fail to decrypt and throw an exception
            // or use default values depending on implementation
            Should.Throw<System.IO.InvalidDataException>(() =>
            {
                WritableConfig.Initialize<TestSettings>(options =>
                {
                    options.FileName = testFileName;
                    options.Provider = new WritableConfigEncryptProvider(encryptionKey2);
                });
            });
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_WithEncryptProvider_ShouldWork()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        var encryptionKey = "asyncencryptionkey12345678901";

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = testFileName;
                options.Provider = new WritableConfigEncryptProvider(encryptionKey);
            });

            await WritableConfig.SaveAsync<TestSettings>(settings =>
            {
                settings.Name = "async_encrypt_test";
                settings.Value = 666;
                settings.SecretKey = "asyncsecret";
            });

            File.Exists(testFileName).ShouldBeTrue();

            var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
            loadedSettings.Name.ShouldBe("async_encrypt_test");
            loadedSettings.Value.ShouldBe(666);
            loadedSettings.SecretKey.ShouldBe("asyncsecret");
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }
}