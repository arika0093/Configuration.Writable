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
        WritableConfig.Initialize<TestSettings>();
        var instance = WritableConfig.GetInstance<TestSettings>();
        instance.ShouldNotBeNull();
        instance.ShouldBeAssignableTo<IWritableOptions<TestSettings>>();
    }

    [Fact]
    public void GetInstance_ShouldThrowIfNotInitialized()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var instance = WritableConfig.GetInstance<TestSettings2>();
        });
    }

    [Fact]
    public void Save_ShouldPersistConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

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

        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("updated");
        loadedSettings.Value.ShouldBe(100);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

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

        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("async_updated");
        loadedSettings.Value.ShouldBe(200);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void SaveWithAction_ShouldUpdateConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

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

    [Fact]
    public void Save_WithColonSeparatedSectionName_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.SectionRootName = "App:Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "nested_test",
            Value = 123,
            IsEnabled = true,
        };

        WritableConfig.Save(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"App\"");
        fileContent.ShouldContain("\"Settings\"");
        fileContent.ShouldContain("\"nested_test\"");
        fileContent.ShouldContain("123");

        // Verify the nested structure
        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("nested_test");
        loadedSettings.Value.ShouldBe(123);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Save_WithUnderscoreSeparatedSectionName_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.SectionRootName = "Database__Connection";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "db_test",
            Value = 456,
            IsEnabled = false,
        };

        WritableConfig.Save(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"Database\"");
        fileContent.ShouldContain("\"Connection\"");
        fileContent.ShouldContain("\"db_test\"");
        fileContent.ShouldContain("456");

        // Verify the nested structure
        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("db_test");
        loadedSettings.Value.ShouldBe(456);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Save_WithMultiLevelNestedSectionName_ShouldCreateDeepNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.SectionRootName = "App:Database:Connection:Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "deep_nested",
            Value = 789,
            IsEnabled = true,
        };

        WritableConfig.Save(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"App\"");
        fileContent.ShouldContain("\"Database\"");
        fileContent.ShouldContain("\"Connection\"");
        fileContent.ShouldContain("\"Settings\"");
        fileContent.ShouldContain("\"deep_nested\"");

        // Verify the nested structure
        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("deep_nested");
        loadedSettings.Value.ShouldBe(789);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Save_WithMixedSeparators_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        WritableConfig.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.SectionRootName = "App:Config__Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "mixed_separators",
            Value = 999,
            IsEnabled = false,
        };

        WritableConfig.Save(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"App\"");
        fileContent.ShouldContain("\"Config\"");
        fileContent.ShouldContain("\"Settings\"");
        fileContent.ShouldContain("\"mixed_separators\"");

        // Verify the nested structure
        var loadedSettings = WritableConfig.GetCurrentValue<TestSettings>();
        loadedSettings.Name.ShouldBe("mixed_separators");
        loadedSettings.Value.ShouldBe(999);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }
}

file class TestSettings
{
    public string Name { get; set; } = "default";
    public int Value { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
}

file class TestSettings2 : TestSettings;
