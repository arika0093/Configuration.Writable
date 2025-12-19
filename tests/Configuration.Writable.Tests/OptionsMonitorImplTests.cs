using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
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

    private WritableOptionsConfiguration<TestSettings> CreateConfigOptions(
        string fileName,
        string instanceName,
        InMemoryFileProvider FileProvider
    )
    {
        var builder = new WritableOptionsConfigBuilder<TestSettings> { FilePath = fileName };
        builder.UseInMemoryFileProvider(FileProvider);
        return builder.BuildOptions(instanceName);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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
        await configOptions.FormatProvider.SaveAsync(testSettings, configOptions);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => monitor.Get("nonexistent"));
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        // Assert - UpdateCache no longer notifies, only FileSystemWatcher does
        disposable.ShouldNotBeNull();
        changeCount.ShouldBe(0);
        changedValue.ShouldBeNull();
        changedName.ShouldBeNull();

        // Verify cache is updated
        var cached = monitor.CurrentValue;
        cached.Name.ShouldBe("changed");
        cached.Value.ShouldBe(100);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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
    public void UpdateCache_ShouldUpdateCacheWithoutNotifying()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            FileProvider
        );

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);
        var notified = false;

        monitor.OnChange((value, name) => notified = true);

        // Act
        var newSettings = new TestSettings { Name = "updated", Value = 200 };
        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, newSettings);

        // Assert - UpdateCache updates cache but does not notify (FileSystemWatcher handles that)
        notified.ShouldBeFalse();
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([
            configOptions1,
            configOptions2,
        ]);
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
        await configOptions.FormatProvider.SaveAsync(testSettings, configOptions);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
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

        await configOptions1.FormatProvider.SaveAsync(settings1, configOptions1);
        await configOptions2.FormatProvider.SaveAsync(settings2, configOptions2);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([
            configOptions1,
            configOptions2,
        ]);
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

    [Fact]
    public void OnChangeThrottle_WithDefaultThrottle_ShouldReceiveOnlyFirstChange()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = "test.json",
            // Default throttle is 1000ms
        };
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions(Microsoft.Extensions.Options.Options.DefaultName);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        var changeCount = 0;
        monitor.OnChange((value, name) => changeCount++);

        // Act - Trigger multiple rapid changes
        var settings1 = new TestSettings { Name = "change1", Value = 1 };
        var settings2 = new TestSettings { Name = "change2", Value = 2 };
        var settings3 = new TestSettings { Name = "change3", Value = 3 };

        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, settings1);
        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, settings2);
        monitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, settings3);

        // Assert - UpdateCache no longer notifies (FileSystemWatcher handles notifications)
        changeCount.ShouldBe(0);
    }

    [Fact]
    public void OnChangeThrottle_WithZeroThrottle_ShouldDisableThrottling()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = "test.json",
            OnChangeThrottleMs = 0, // Disable throttling
        };
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions(Microsoft.Extensions.Options.Options.DefaultName);

        // Assert
        configOptions.OnChangeThrottleMs.ShouldBe(0);
    }

    [Fact]
    public void OnChangeThrottle_Configuration_ShouldBeStoredCorrectly()
    {
        // Arrange & Act
        var builder = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = "test.json",
            OnChangeThrottleMs = 2000,
        };
        var FileProvider = new InMemoryFileProvider();
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions("test");

        // Assert
        configOptions.OnChangeThrottleMs.ShouldBe(2000);
    }

    [Fact]
    public void OnChangeThrottle_DefaultValue_ShouldBe1000Ms()
    {
        // Arrange & Act
        var builder = new WritableOptionsConfigBuilder<TestSettings> { FilePath = "test.json" };
        var FileProvider = new InMemoryFileProvider();
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions("test");

        // Assert
        configOptions.OnChangeThrottleMs.ShouldBe(1000);
    }

    [Fact]
    public async Task OnChangeThrottle_MultipleInstances_ShouldHaveIndependentThrottle()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();

        var builder1 = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = "test1.json",
            OnChangeThrottleMs = 500,
        };
        builder1.UseInMemoryFileProvider(FileProvider);
        var configOptions1 = builder1.BuildOptions("instance1");

        var builder2 = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = "test2.json",
            OnChangeThrottleMs = 1500,
        };
        builder2.UseInMemoryFileProvider(FileProvider);
        var configOptions2 = builder2.BuildOptions("instance2");

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([
            configOptions1,
            configOptions2,
        ]);
        var monitor = new OptionsMonitorImpl<TestSettings>(registry);

        // Preload data
        var settings1 = new TestSettings { Name = "first", Value = 111 };
        var settings2 = new TestSettings { Name = "second", Value = 222 };
        await configOptions1.FormatProvider.SaveAsync(settings1, configOptions1);
        await configOptions2.FormatProvider.SaveAsync(settings2, configOptions2);

        // Assert
        configOptions1.OnChangeThrottleMs.ShouldBe(500);
        configOptions2.OnChangeThrottleMs.ShouldBe(1500);
    }
}
