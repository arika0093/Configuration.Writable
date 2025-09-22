using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableConfigTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
    }

    [Fact]
    public void Initialize_ShouldCreateConfiguration()
    {
        WritableConfig.Initialize<TestSettings>();

        var settings = WritableConfig.GetCurrentValue<TestSettings>();
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void GetInstance_ShouldReturnWritableConfig()
    {
        var instance = WritableConfig.GetInstance<TestSettings>();
        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<WritableConfig<TestSettings>>();
    }

    [Fact]
    public void Save_ShouldPersistConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var newSettings = new TestSettings
            {
                Name = "updated",
                Value = 100,
                IsEnabled = false
            };

            WritableConfig.Save(newSettings);

            File.Exists(testFileName).ShouldBeTrue();

            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
            loadedSettings.Name.ShouldBe("updated");
            loadedSettings.Value.ShouldBe(100);
            loadedSettings.IsEnabled.ShouldBeFalse();
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
    public async Task SaveAsync_ShouldPersistConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var newSettings = new TestSettings
            {
                Name = "async_updated",
                Value = 200,
                IsEnabled = false
            };

            await WritableConfig.SaveAsync(newSettings);

            File.Exists(testFileName).ShouldBeTrue();

            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
            loadedSettings.Name.ShouldBe("async_updated");
            loadedSettings.Value.ShouldBe(200);
            loadedSettings.IsEnabled.ShouldBeFalse();
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
    public void SaveWithAction_ShouldUpdateConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            WritableConfig.Save<TestSettings>(settings =>
            {
                settings.Name = "action_updated";
                settings.Value = 300;
            });

            File.Exists(testFileName).ShouldBeTrue();

            WritableConfig.Initialize<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
            loadedSettings.Name.ShouldBe("action_updated");
            loadedSettings.Value.ShouldBe(300);
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
    public void GetConfigFilePath_ShouldReturnCorrectPath()
    {
        WritableConfig.Initialize<TestSettings>();

        var path = WritableConfig.GetConfigFilePath<TestSettings>();
        path.ShouldNotBeNullOrEmpty();
        path.ShouldEndWith(".json");
    }
}