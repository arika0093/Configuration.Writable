using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableConfigTests
{
    private readonly InMemoryFileWriter _fileWriter = new();
    private WritableConfigSimpleInstance _instance = null!;

    public WritableConfigTests()
    {
        _instance = new WritableConfigSimpleInstance();
    }

    [Fact]
    public void Initialize_ShouldCreateConfiguration()
    {
        _instance.Initialize<TestSettings>();

        var option = _instance.GetOption<TestSettings>();
        var settings = option.CurrentValue;
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void GetOption_ShouldReturnWritableConfig()
    {
        _instance.Initialize<TestSettings>();
        var option = _instance.GetOption<TestSettings>();
        option.ShouldNotBeNull();
        option.ShouldBeAssignableTo<IWritableOptions<TestSettings>>();
    }

    [Fact]
    public void GetOption_ShouldThrowIfNotInitialized()
    {
        var uninitializedInstance = new WritableConfigSimpleInstance();
        Should.Throw<InvalidOperationException>(() =>
        {
            var instance = uninitializedInstance.GetOption<TestSettings2>();
        });
    }

    [Fact]
    public async Task Save_ShouldPersistConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
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

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("updated");
        loadedSettings.Value.ShouldBe(100);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
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

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("async_updated");
        loadedSettings.Value.ShouldBe(200);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveWithAction_ShouldUpdateConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(settings =>
        {
            settings.Name = "action_updated";
            settings.Value = 300;
        });

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("action_updated");
        loadedSettings.Value.ShouldBe(300);
    }

    [Fact]
    public void GetConfigFilePath_ShouldReturnCorrectPath()
    {
        _instance.Initialize<TestSettings>();

        var option = _instance.GetOption<TestSettings>();
        var path = option.GetConfigurationOptions().ConfigFilePath;
        path.ShouldNotBeNullOrEmpty();
        path.ShouldEndWith(".json");
    }

    [Fact]
    public async Task Save_WithColonSeparatedSectionName_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
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

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"App\"");
        fileContent.ShouldContain("\"Settings\"");
        fileContent.ShouldContain("\"nested_test\"");
        fileContent.ShouldContain("123");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("nested_test");
        loadedSettings.Value.ShouldBe(123);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_WithUnderscoreSeparatedSectionName_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
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

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"Database\"");
        fileContent.ShouldContain("\"Connection\"");
        fileContent.ShouldContain("\"db_test\"");
        fileContent.ShouldContain("456");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("db_test");
        loadedSettings.Value.ShouldBe(456);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Save_WithMultiLevelNestedSectionName_ShouldCreateDeepNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
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

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"App\"");
        fileContent.ShouldContain("\"Database\"");
        fileContent.ShouldContain("\"Connection\"");
        fileContent.ShouldContain("\"Settings\"");
        fileContent.ShouldContain("\"deep_nested\"");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("deep_nested");
        loadedSettings.Value.ShouldBe(789);
        loadedSettings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_WithMixedSeparators_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
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

        var option = _instance.GetOption<TestSettings>();
        await option.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _fileWriter.ReadAllText(testFileName);
        fileContent.ShouldContain("\"App\"");
        fileContent.ShouldContain("\"Config\"");
        fileContent.ShouldContain("\"Settings\"");
        fileContent.ShouldContain("\"mixed_separators\"");

        // Verify the nested structure
        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("mixed_separators");
        loadedSettings.Value.ShouldBe(999);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void SaveAsync_OnSynchronizationContext_ShouldNotDeadlock()
    {
        var testFileName = Path.GetRandomFileName();

        _instance.Initialize<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var newSettings = new TestSettings
        {
            Name = "synccontext_test",
            Value = 888,
            IsEnabled = false,
        };

        var option = _instance.GetOption<TestSettings>();

        // Simulate a synchronization context that could cause deadlock
        var previousContext = SynchronizationContext.Current;
        try
        {
            var mockContext = new MockSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(mockContext);

            // This should not deadlock even with a synchronization context
            option.SaveAsync(newSettings).Wait();

            _fileWriter.FileExists(testFileName).ShouldBeTrue();

            var loadedSettings = option.CurrentValue;
            loadedSettings.Name.ShouldBe("synccontext_test");
            loadedSettings.Value.ShouldBe(888);
            loadedSettings.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }
}

file class TestSettings
{
    public string Name { get; set; } = "default";
    public int Value { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
}

file class TestSettings2 : TestSettings;

file class MockSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        // Execute synchronously to simulate a context that could cause deadlock
        d(state);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        d(state);
    }
}
