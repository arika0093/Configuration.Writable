using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;

namespace Configuration.Writable.Yaml.Tests;

public class WritableConfigYamlProviderTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
        public string[] Items { get; set; } = ["item1", "item2"];
        public NestedSettings Nested { get; set; } = new();
    }

    public class NestedSettings
    {
        public string Description { get; set; } = "nested_default";
        public double Price { get; set; } = 19.99;
    }

    [Fact]
    public void WritableConfigYamlProvider_ShouldHaveCorrectFileExtension()
    {
        var provider = new WritableConfigYamlProvider();
        provider.FileExtension.ShouldBe("yaml");
    }

    [Fact]
    public async Task Initialize_WithYamlProvider_ShouldCreateYamlFile()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var settings = new TestSettings
        {
            Name = "yaml_test",
            Value = 456,
            IsEnabled = false,
            Items = ["yaml1", "yaml2", "yaml3"],
            Nested = new NestedSettings { Description = "nested_yaml_test", Price = 99.99 },
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(settings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("yaml_test");
        fileContent.ShouldContain("456");
        fileContent.ShouldContain("false");
        fileContent.ShouldContain("nested_yaml_test");
        fileContent.ShouldContain("99.99");
    }

    [Fact]
    public async Task LoadAndSave_WithYamlProvider_ShouldPreserveData()
    {
        var testFileName = Path.GetRandomFileName();
        var provider = new WritableConfigYamlProvider();
        provider.FileWriter = _fileWriter;

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = provider;
        });

        var originalSettings = new TestSettings
        {
            Name = "yaml_persistence_test",
            Value = 789,
            IsEnabled = true,
            Items = ["yaml_persist1", "yaml_persist2"],
            Nested = new NestedSettings { Description = "nested_persist", Price = 123.45 },
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(originalSettings);

        // Re-initialize with the same provider to simulate reloading
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = provider;
        });

        option = _instance.GetOptions();
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("yaml_persistence_test");
        loadedSettings.Value.ShouldBe(789);
        loadedSettings.IsEnabled.ShouldBeTrue();
        loadedSettings.Items.ShouldBe(new[] { "yaml_persist1", "yaml_persist2" });
        loadedSettings.Nested.Description.ShouldBe("nested_persist");
        loadedSettings.Nested.Price.ShouldBe(123.45);
    }

    [Fact]
    public async Task SaveAsync_WithYamlProvider_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = _instance.GetOptions();
        await option.SaveAsync(settings =>
        {
            settings.Name = "async_yaml_test";
            settings.Value = 888;
            settings.Nested.Description = "async_nested";
        });

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("async_yaml_test");
        loadedSettings.Value.ShouldBe(888);
        loadedSettings.Nested.Description.ShouldBe("async_nested");
    }

    [Fact]
    public async Task Save_WithColonSeparatedSectionName_ShouldCreateNestedYaml()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.SectionName = "App:Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "yaml_nested_test",
            Value = 123,
            IsEnabled = true,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("app:");
        fileContent.ShouldContain("settings:");
        fileContent.ShouldContain("yaml_nested_test");
        fileContent.ShouldContain("123");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("yaml_nested_test");
        loadedSettings.Value.ShouldBe(123);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_WithUnderscoreSeparatedSectionName_ShouldCreateNestedYaml()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.SectionName = "Database__Connection";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "yaml_db_test",
            Value = 456,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("database:");
        fileContent.ShouldContain("connection:");
        fileContent.ShouldContain("yaml_db_test");
        fileContent.ShouldContain("456");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("yaml_db_test");
        loadedSettings.Value.ShouldBe(456);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Save_WithMultiLevelNestedSectionName_ShouldCreateDeepNestedYaml()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.Provider = new WritableConfigYamlProvider();
            options.SectionName = "App:Database:Connection:Settings";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "yaml_deep_nested",
            Value = 789,
            IsEnabled = true,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("app:");
        fileContent.ShouldContain("database:");
        fileContent.ShouldContain("connection:");
        fileContent.ShouldContain("settings:");
        fileContent.ShouldContain("yaml_deep_nested");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("yaml_deep_nested");
        loadedSettings.Value.ShouldBe(789);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }
}
