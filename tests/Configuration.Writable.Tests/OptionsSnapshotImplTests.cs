using System;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Configuration.Writable.Options;

namespace Configuration.Writable.Tests;

public class OptionsSnapshotImplTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
    }

    private WritableConfigurationOptions<TestSettings> CreateConfigOptions(
        string fileName,
        string instanceName,
        InMemoryFileWriter fileWriter
    )
    {
        var builder = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            FilePath = fileName,
            InstanceName = instanceName,
        };
        builder.UseInMemoryFileWriter(fileWriter);
        return builder.BuildOptions();
    }

    [Fact]
    public void Value_ShouldReturnSnapshotValue()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            fileWriter
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Act
        var value = snapshot.Value;

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public void Get_WithDefaultName_ShouldReturnSnapshotValue()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            fileWriter
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Act
        var value = snapshot.Get(Microsoft.Extensions.Options.Options.DefaultName);

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public async Task Get_WithCustomName_ShouldReturnCustomValue()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions("test.json", "custom", fileWriter);

        // Preload custom data
        var testSettings = new TestSettings { Name = "custom", Value = 999 };
        await configOptions.Provider.SaveAsync(testSettings, new OptionOperations<TestSettings>(), configOptions);

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Act
        var value = snapshot.Get("custom");

        // Assert
        value.Name.ShouldBe("custom");
        value.Value.ShouldBe(999);
    }

    [Fact]
    public void Snapshot_ShouldNotReflectChangesAfterCreation()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            fileWriter
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Get initial value
        var initialValue = snapshot.Value;
        initialValue.Name.ShouldBe("default");
        initialValue.Value.ShouldBe(42);

        // Act - Update monitor cache
        var newSettings = new TestSettings { Name = "changed", Value = 100 };
        optionsMonitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, newSettings);

        // Assert - Snapshot should still have the original value
        var snapshotValue = snapshot.Value;
        snapshotValue.Name.ShouldBe("default");
        snapshotValue.Value.ShouldBe(42);

        // But monitor should have the new value
        var monitorValue = optionsMonitor.CurrentValue;
        monitorValue.Name.ShouldBe("changed");
        monitorValue.Value.ShouldBe(100);
    }

    [Fact]
    public async Task Snapshot_WithMultipleInstances_ShouldSnapshotAllInstances()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();

        var configOptions1 = CreateConfigOptions("test1.json", "instance1", fileWriter);
        var configOptions2 = CreateConfigOptions("test2.json", "instance2", fileWriter);

        // Preload different data for each instance
        var settings1 = new TestSettings { Name = "first", Value = 111 };
        var settings2 = new TestSettings { Name = "second", Value = 222 };

        await configOptions1.Provider.SaveAsync(settings1, new OptionOperations<TestSettings>(), configOptions1);
        await configOptions2.Provider.SaveAsync(settings2, new OptionOperations<TestSettings>(), configOptions2);

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(
            new[] { configOptions1, configOptions2 }
        );
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Act
        var value1 = snapshot.Get("instance1");
        var value2 = snapshot.Get("instance2");

        // Assert
        value1.Name.ShouldBe("first");
        value1.Value.ShouldBe(111);
        value2.Name.ShouldBe("second");
        value2.Value.ShouldBe(222);
    }

    [Fact]
    public void Snapshot_MultipleCalls_ShouldReturnSameInstance()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            fileWriter
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Act
        var value1 = snapshot.Value;
        var value2 = snapshot.Value;

        // Assert
        value1.ShouldBeSameAs(value2);
    }

    [Fact]
    public void Snapshot_AfterMonitorUpdate_NewSnapshotShouldStillHaveDefaultValue()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            fileWriter
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });

        // Create first snapshot
        var snapshot1 = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);
        var value1 = snapshot1.Value;
        value1.Name.ShouldBe("default");
        value1.Value.ShouldBe(42);

        // Update monitor
        var newSettings = new TestSettings { Name = "updated", Value = 200 };
        optionsMonitor.UpdateCache(Microsoft.Extensions.Options.Options.DefaultName, newSettings);

        // Create second snapshot after update
        var snapshot2 = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);
        var value2 = snapshot2.Value;

        // Assert
        // First snapshot should still have old value
        snapshot1.Value.Name.ShouldBe("default");
        snapshot1.Value.Value.ShouldBe(42);

        // Second snapshot should also have default value (from GetDefaultValue)
        // Note: OptionsSnapshotImpl uses GetDefaultValue which returns the value
        // captured at OptionsMonitorImpl initialization, not the cached value
        value2.Name.ShouldBe("default");
        value2.Value.ShouldBe(42);

        // But monitor should have the updated cached value
        var monitorValue = optionsMonitor.CurrentValue;
        monitorValue.Name.ShouldBe("updated");
        monitorValue.Value.ShouldBe(200);
    }

    [Fact]
    public void Get_WithNull_ShouldThrow()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var configOptions = CreateConfigOptions(
            "test.json",
            Microsoft.Extensions.Options.Options.DefaultName,
            fileWriter
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);

        // Act & Assert
        Should.Throw<Exception>(() => snapshot.Get(null!));
    }

    [Fact]
    public async Task Snapshot_ShouldCaptureAllInstancesAtCreationTime()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();

        var configOptions = CreateConfigOptions("test.json", "instance1", fileWriter);

        // Initial data
        var initialSettings = new TestSettings { Name = "initial", Value = 100 };
        await configOptions.Provider.SaveAsync(initialSettings, new OptionOperations<TestSettings>(), configOptions);

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });

        // Create snapshot with initial data
        var snapshot = new OptionsSnapshotImpl<TestSettings>(optionsMonitor);
        var snapshotValue = snapshot.Get("instance1");
        snapshotValue.Name.ShouldBe("initial");
        snapshotValue.Value.ShouldBe(100);

        // Update monitor
        var updatedSettings = new TestSettings { Name = "updated", Value = 999 };
        optionsMonitor.UpdateCache("instance1", updatedSettings);

        // Assert - Snapshot should still have initial value
        var stillSnapshotValue = snapshot.Get("instance1");
        stillSnapshotValue.Name.ShouldBe("initial");
        stillSnapshotValue.Value.ShouldBe(100);

        // Monitor should have updated value
        var monitorValue = optionsMonitor.Get("instance1");
        monitorValue.Name.ShouldBe("updated");
        monitorValue.Value.ShouldBe(999);
    }
}
