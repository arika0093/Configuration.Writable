using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableConfigTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

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

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "updated",
            Value = 100,
            IsEnabled = false,
        };

        WritableConfig.Save(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("updated");
        loadedSettings.Value.ShouldBe(100);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "async_updated",
            Value = 200,
            IsEnabled = false,
        };

        await WritableConfig.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("async_updated");
        loadedSettings.Value.ShouldBe(200);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void SaveWithAction_ShouldUpdateConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        WritableConfig.Save<TestSettings>(settings =>
        {
            settings.Name = "action_updated";
            settings.Value = 300;
        });

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("action_updated");
        loadedSettings.Value.ShouldBe(300);
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
