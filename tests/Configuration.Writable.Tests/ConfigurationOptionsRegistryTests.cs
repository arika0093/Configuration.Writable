using System;
using System.Collections.Generic;
using System.Linq;
using Configuration.Writable.Configure;
using Configuration.Writable.Options;

namespace Configuration.Writable.Tests;

public class ConfigurationOptionsRegistryTests
{
    private class TestSettings
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    [Fact]
    public void Constructor_InitializesWithProvidedOptions()
    {
        // Arrange
        var options1 = CreateOptions("instance1", "file1.json");
        var options2 = CreateOptions("instance2", "file2.json");
        var optionsList = new[] { options1, options2 };

        // Act
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>(optionsList);

        // Assert
        var instanceNames = registry.GetInstanceNames().ToList();
        instanceNames.ShouldContain("instance1");
        instanceNames.ShouldContain("instance2");
        instanceNames.Count.ShouldBe(2);
    }

    [Fact]
    public void Get_ReturnsCorrectOption()
    {
        // Arrange
        var options1 = CreateOptions("instance1", "file1.json");
        var options2 = CreateOptions("instance2", "file2.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([options1, options2]);

        // Act
        var retrieved1 = registry.Get("instance1");
        var retrieved2 = registry.Get("instance2");

        // Assert
        retrieved1.InstanceName.ShouldBe("instance1");
        retrieved1.ConfigFilePath.ShouldContain("file1.json");
        retrieved2.InstanceName.ShouldBe("instance2");
        retrieved2.ConfigFilePath.ShouldContain("file2.json");
    }

    [Fact]
    public void Get_ThrowsKeyNotFoundException_WhenInstanceNotFound()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);

        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => registry.Get("nonexistent"));
    }

    [Fact]
    public void TryAdd_AddsNewOption_ReturnsTrue()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);

        // Act
        var result = registry.TryAdd(opt =>
        {
            opt.InstanceName = "newInstance";
            opt.FilePath = "new.json";
        });

        // Assert
        result.ShouldBeTrue();
        registry.GetInstanceNames().ShouldContain("newInstance");
        var addedOption = registry.Get("newInstance");
        addedOption.InstanceName.ShouldBe("newInstance");
        addedOption.ConfigFilePath.ShouldContain("new.json");
    }

    [Fact]
    public void TryAdd_WhenInstanceExists_ReturnsFalse()
    {
        // Arrange
        var existingOption = CreateOptions("existing", "existing.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([existingOption]);

        // Act
        var result = registry.TryAdd(opt =>
        {
            opt.InstanceName = "existing";
            opt.FilePath = "new.json";
        });

        // Assert
        result.ShouldBeFalse();
        var option = registry.Get("existing");
        option.ConfigFilePath.ShouldContain("existing.json"); // Should not be changed
    }

    [Fact]
    public void TryAdd_TriggersOnAddedEvent()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);
        WritableOptionsConfiguration<TestSettings>? addedOption = null;
        registry.OnAdded += opt => addedOption = opt;

        // Act
        registry.TryAdd(opt =>
        {
            opt.InstanceName = "newInstance";
            opt.FilePath = "new.json";
        });

        // Assert
        addedOption.ShouldNotBeNull();
        addedOption!.InstanceName.ShouldBe("newInstance");
        addedOption.ConfigFilePath.ShouldContain("new.json");
    }

    [Fact]
    public void TryAdd_WhenFails_DoesNotTriggerOnAddedEvent()
    {
        // Arrange
        var existingOption = CreateOptions("existing", "existing.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([existingOption]);
        var eventTriggered = false;
        registry.OnAdded += _ => eventTriggered = true;

        // Act
        registry.TryAdd(opt =>
        {
            opt.InstanceName = "existing";
            opt.FilePath = "new.json";
        });

        // Assert
        eventTriggered.ShouldBeFalse();
    }

    [Fact]
    public void TryRemove_RemovesExistingOption_ReturnsTrue()
    {
        // Arrange
        var option1 = CreateOptions("instance1", "file1.json");
        var option2 = CreateOptions("instance2", "file2.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([option1, option2]);

        // Act
        var result = registry.TryRemove("instance1");

        // Assert
        result.ShouldBeTrue();
        registry.GetInstanceNames().ShouldNotContain("instance1");
        registry.GetInstanceNames().ShouldContain("instance2");
    }

    [Fact]
    public void TryRemove_WhenInstanceNotFound_ReturnsFalse()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);

        // Act
        var result = registry.TryRemove("nonexistent");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryRemove_TriggersOnRemovedEvent()
    {
        // Arrange
        var option = CreateOptions("instance1", "file1.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([option]);
        string? removedInstanceName = null;
        registry.OnRemoved += name => removedInstanceName = name;

        // Act
        registry.TryRemove("instance1");

        // Assert
        removedInstanceName.ShouldBe("instance1");
    }

    [Fact]
    public void TryRemove_WhenFails_DoesNotTriggerOnRemovedEvent()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);
        var eventTriggered = false;
        registry.OnRemoved += _ => eventTriggered = true;

        // Act
        registry.TryRemove("nonexistent");

        // Assert
        eventTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Clear_RemovesAllOptions()
    {
        // Arrange
        var option1 = CreateOptions("instance1", "file1.json");
        var option2 = CreateOptions("instance2", "file2.json");
        var option3 = CreateOptions("instance3", "file3.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([option1, option2, option3]);

        // Act
        registry.Clear();

        // Assert
        registry.GetInstanceNames().ShouldBeEmpty();
    }

    [Fact]
    public void Clear_TriggersOnRemovedEventForEachItem()
    {
        // Arrange
        var option1 = CreateOptions("instance1", "file1.json");
        var option2 = CreateOptions("instance2", "file2.json");
        var option3 = CreateOptions("instance3", "file3.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([option1, option2, option3]);
        var removedInstances = new List<string>();
        registry.OnRemoved += name => removedInstances.Add(name);

        // Act
        registry.Clear();

        // Assert
        removedInstances.Count.ShouldBe(3);
        removedInstances.ShouldContain("instance1");
        removedInstances.ShouldContain("instance2");
        removedInstances.ShouldContain("instance3");
    }

    [Fact]
    public void Clear_OnEmptyRegistry_DoesNothing()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);
        var eventTriggered = false;
        registry.OnRemoved += _ => eventTriggered = true;

        // Act
        registry.Clear();

        // Assert
        eventTriggered.ShouldBeFalse();
        registry.GetInstanceNames().ShouldBeEmpty();
    }

    [Fact]
    public void GetInstanceNames_ReturnsEmptyForEmptyRegistry()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);

        // Act
        var names = registry.GetInstanceNames();

        // Assert
        names.ShouldBeEmpty();
    }

    [Fact]
    public void MultipleEventHandlers_AllGetTriggered()
    {
        // Arrange
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([]);
        var addedCount = 0;
        var removedCount = 0;

        registry.OnAdded += _ => addedCount++;
        registry.OnAdded += _ => addedCount++;
        registry.OnRemoved += _ => removedCount++;
        registry.OnRemoved += _ => removedCount++;

        // Act
        registry.TryAdd(opt =>
        {
            opt.InstanceName = "test";
            opt.FilePath = "test.json";
        });
        registry.TryRemove("test");

        // Assert
        addedCount.ShouldBe(2);
        removedCount.ShouldBe(2);
    }

    [Fact]
    public void ComplexScenario_AddRemoveMultipleOperations()
    {
        // Arrange
        var option1 = CreateOptions("initial", "initial.json");
        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([option1]);

        // Act & Assert - Add multiple
        registry
            .TryAdd(opt =>
            {
                opt.InstanceName = "second";
                opt.FilePath = "second.json";
            })
            .ShouldBeTrue();
        registry
            .TryAdd(opt =>
            {
                opt.InstanceName = "third";
                opt.FilePath = "third.json";
            })
            .ShouldBeTrue();
        registry.GetInstanceNames().Count().ShouldBe(3);

        // Remove one
        registry.TryRemove("second").ShouldBeTrue();
        registry.GetInstanceNames().Count().ShouldBe(2);
        registry.GetInstanceNames().ShouldNotContain("second");

        // Try to add duplicate
        registry
            .TryAdd(opt =>
            {
                opt.InstanceName = "initial";
                opt.FilePath = "new.json";
            })
            .ShouldBeFalse();
        registry.Get("initial").ConfigFilePath.ShouldContain("initial.json");

        // Add removed one back
        registry
            .TryAdd(opt =>
            {
                opt.InstanceName = "second";
                opt.FilePath = "second-new.json";
            })
            .ShouldBeTrue();
        registry.Get("second").ConfigFilePath.ShouldContain("second-new.json");

        // Clear all
        registry.Clear();
        registry.GetInstanceNames().ShouldBeEmpty();
    }

    // Helper method to create WritableOptionsConfiguration
    private static WritableOptionsConfiguration<TestSettings> CreateOptions(
        string instanceName,
        string filePath
    )
    {
        var builder = new WritableOptionsConfigBuilder<TestSettings>
        {
            InstanceName = instanceName,
            FilePath = filePath,
        };
        return builder.BuildOptions();
    }
}
