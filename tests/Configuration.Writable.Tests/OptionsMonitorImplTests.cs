using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Options;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Tests;

public class OptionsMonitorImplTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
    }

    private WritableConfigurationOptions<TestSettings> CreateConfigOptions(
        string fileName,
        string instanceName,
        InMemoryFileProvider FileProvider
    )
    {
        var builder = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            FilePath = fileName,
            InstanceName = instanceName,
        };
        builder.UseInMemoryFileProvider(FileProvider);
        return builder.BuildOptions();
    }

    [Fact]
    public void CurrentValue_ShouldReturnDefaultValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var value = monitor.CurrentValue;

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public void Get_WithDefaultName_ShouldReturnCurrentValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var value = monitor.Get(Microsoft.Extensions.Options.Options.DefaultName);

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public void Get_WithNull_ShouldReturnDefaultValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var value = monitor.Get(null);

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public async Task Get_WithCustomName_ShouldReturnCustomValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions("test.json", "custom", FileProvider);

        // Preload custom data
        var testSettings = new TestSettings { Name = "custom", Value = 999 };
        await configOptions.Provider.SaveAsync(
            testSettings,
            new OptionOperations<TestSettings>(),
            configOptions
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var value = monitor.Get("custom");

        // Assert
        value.Name.ShouldBe("custom");
        value.Value.ShouldBe(999);
    }

    [Fact]
    public void Get_WithInvalidName_ShouldThrow()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => monitor.Get("nonexistent"));
    }

    [Fact]
    public void Get_MultipleCalls_ShouldReturnCachedValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var value1 = monitor.Get(Microsoft.Extensions.Options.Options.DefaultName);
        var value2 = monitor.Get(Microsoft.Extensions.Options.Options.DefaultName);

        // Assert
        value1.ShouldBeSameAs(value2);
    }

    [Fact]
    public void OnChange_ShouldRegisterListener()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);
        var changeCount = 0;
        TestSettings? changedValue = null;
        string? changedName = null;

        // Act
        var disposable = monitor.OnChange(
            (value, name) =>
            {
                changeCount++;
                changedValue = value;
                changedName = name;
            }
        );

        // Trigger change
        var newSettings = new TestSettings { Name = "changed", Value = 100 };
        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, newSettings);

        // Assert
        disposable.ShouldNotBeNull();
        changeCount.ShouldBe(1);
        changedValue.ShouldNotBeNull();
        changedValue.Name.ShouldBe("changed");
        changedName.ShouldBe(Microsoft.Extensions.Options.Options.DefaultName);
    }

    [Fact]
    public void OnChange_DisposingListener_ShouldUnregister()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);
        var changeCount = 0;

        var disposable = monitor.OnChange((value, name) => changeCount++);

        // Act
        disposable?.Dispose();
        var newSettings = new TestSettings { Name = "changed", Value = 100 };
        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, newSettings);

        // Assert
        changeCount.ShouldBe(0);
    }

    [Fact]
    public void UpdateCache_ShouldUpdateValueAndNotifyListeners()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);
        var notified = false;

        monitor.OnChange((value, name) => notified = true);

        // Act
        var newSettings = new TestSettings { Name = "updated", Value = 200 };
        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, newSettings);

        // Assert
        notified.ShouldBeTrue();
        var value = monitor.CurrentValue;
        value.Name.ShouldBe("updated");
        value.Value.ShouldBe(200);
    }

    [Fact]
    public void ClearCache_ShouldRemoveCachedValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Get value to cache it
        var initialValue = monitor.CurrentValue;
        initialValue.ShouldNotBeNull();

        // Act
        monitor.ClearCache(Microsoft.Extensions.Options.Options.DefaultName);
        var newValue = monitor.CurrentValue;

        // Assert - should create new instance after cache clear
        newValue.ShouldNotBeNull();
        // Note: instances will be different due to new deserialization
    }

    [Fact]
    public void GetInstanceNames_ShouldReturnAllConfiguredNames()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions1 = CreateConfigOptions("test1.json", "instance1", FileProvider);
        var configOptions2 = CreateConfigOptions("test2.json", "instance2", FileProvider);

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>(
            [configOptions1, configOptions2]
        );
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var names = monitor.GetInstanceNames();

        // Assert
        names.ShouldContain("instance1");
        names.ShouldContain("instance2");
    }

    [Fact]
    public async Task GetDefaultValue_ShouldReturnStoredDefaultValue()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var testSettings = new TestSettings { Name = "preloaded", Value = 555 };

        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        // Preload data
        await configOptions.Provider.SaveAsync(
            testSettings,
            new OptionOperations<TestSettings>(),
            configOptions
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var defaultValue = monitor.GetDefaultValue(
            Microsoft.Extensions.Options.Options.DefaultName
        );

        // Assert
        defaultValue.Name.ShouldBe("preloaded");
        defaultValue.Value.ShouldBe(555);
    }

    [Fact]
    public void GetDefaultValue_WithInvalidName_ShouldThrow()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => monitor.GetDefaultValue("nonexistent"));
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);
        var changeCount = 0;

        monitor.OnChange((value, name) => changeCount++);

        // Act
        monitor.Dispose();

        // Assert - shouldn't throw
        // Note: Testing file watchers disposal would require more complex setup
    }

    [Fact]
    public async Task MultipleInstances_ShouldWorkIndependently()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();

        var configOptions1 = CreateConfigOptions("test1.json", "instance1", FileProvider);
        var configOptions2 = CreateConfigOptions("test2.json", "instance2", FileProvider);

        // Preload different data for each instance
        var settings1 = new TestSettings { Name = "first", Value = 111 };
        var settings2 = new TestSettings { Name = "second", Value = 222 };

        await configOptions1.Provider.SaveAsync(
            settings1,
            new OptionOperations<TestSettings>(),
            configOptions1
        );
        await configOptions2.Provider.SaveAsync(
            settings2,
            new OptionOperations<TestSettings>(),
            configOptions2
        );

        var registry = new ConfigurationOptionsRegistryImpl<TestSettings>(
            [configOptions1, configOptions2]
        );
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act
        var value1 = monitor.Get("instance1");
        var value2 = monitor.Get("instance2");

        // Assert
        value1.Name.ShouldBe("first");
        value1.Value.ShouldBe(111);
        value2.Name.ShouldBe("second");
        value2.Value.ShouldBe(222);
    }
}
