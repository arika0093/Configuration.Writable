using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Yaml.Tests;

public class YamlFormatProviderTests
{
    private readonly InMemoryFileProvider _FileProvider = new();

    [Fact]
    public void YamlFormatProvider_ShouldHaveCorrectFileExtension()
    {
        var provider = new YamlFormatProvider();
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
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
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

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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
        var provider = new YamlFormatProvider();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = provider;
            options.UseInMemoryFileProvider(_FileProvider);
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
            options.FormatProvider = provider;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        option = _instance.GetOptions();
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("yaml_persistence_test");
        loadedSettings.Value.ShouldBe(789);
        loadedSettings.IsEnabled.ShouldBeTrue();
        loadedSettings.Items.ShouldBe(["yaml_persist1", "yaml_persist2"]);
        loadedSettings.Nested.Description.ShouldBe("nested_persist");
        loadedSettings.Nested.Price.ShouldBe(123.45);
    }

    [Fact]
    public async Task LoadAndSave_WithNonUtf8Encoding_ShouldPreserveData()
    {
        const string testFileName = "utf16_config.yaml";
        var provider = new YamlFormatProvider { Encoding = Encoding.Unicode };
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = provider;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var option = instance.GetOptions();
        await option.SaveAsync(
            new TestSettings
            {
                Name = "utf16_yaml",
                Value = 321,
                Nested = new NestedSettings { Description = "encoded", Price = 12.5 },
            }
        );

        var fileContent = Encoding.Unicode.GetString(_FileProvider.ReadAllBytes(testFileName));
        fileContent.ShouldContain("utf16_yaml");

        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = provider;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var loadedSettings = instance.GetOptions().CurrentValue;
        loadedSettings.Name.ShouldBe("utf16_yaml");
        loadedSettings.Value.ShouldBe(321);
        loadedSettings.Nested.Description.ShouldBe("encoded");
        loadedSettings.Nested.Price.ShouldBe(12.5);
    }

    [Fact]
    public async Task Load_WithDefaultEncoding_ShouldHonorUtf16ByteOrderMark()
    {
        const string testFileName = "utf16_bom_config.yaml";
        const string yaml = """
            name: bom_yaml
            value: 654
            isEnabled: true
            """;
        var preamble = Encoding.Unicode.GetPreamble();
        var content = Encoding.Unicode.GetBytes(yaml);
        var bytes = new byte[preamble.Length + content.Length];
        preamble.CopyTo(bytes, 0);
        content.CopyTo(bytes, preamble.Length);
        await _FileProvider.SaveToFileAsync(testFileName, bytes);

        var instance = new WritableOptionsSimpleInstance<TestSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var loadedSettings = instance.GetOptions().CurrentValue;
        loadedSettings.Name.ShouldBe("bom_yaml");
        loadedSettings.Value.ShouldBe(654);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOptions_WithMalformedYaml_ShouldThrowInsteadOfReturningDefaults()
    {
        const string testFileName = "malformed.yaml";
        const string malformedContent = "name: [unterminated";
        await _FileProvider.SaveToFileAsync(
            testFileName,
            Encoding.UTF8.GetBytes(malformedContent)
        );

        var instance = new WritableOptionsSimpleInstance<TestSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        Should.Throw<FormatException>(() => instance.GetOptions());
        _FileProvider.ReadAllText(testFileName).ShouldBe(malformedContent);
    }

    [Fact]
    public async Task SaveAsync_WithYamlProvider_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var option = _instance.GetOptions();
        await option.SaveAsync(settings =>
        {
            settings.Name = "async_yaml_test";
            settings.Value = 888;
            settings.Nested.Description = "async_nested";
        });

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

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
            options.FormatProvider = new YamlFormatProvider();
            options.SectionName = "App:Settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "yaml_nested_test",
            Value = 123,
            IsEnabled = true,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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
            options.FormatProvider = new YamlFormatProvider();
            options.SectionName = "Database__Connection";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "yaml_db_test",
            Value = 456,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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
            options.FormatProvider = new YamlFormatProvider();
            options.SectionName = "App:Database:Connection:Settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "yaml_deep_nested",
            Value = 789,
            IsEnabled = true,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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
