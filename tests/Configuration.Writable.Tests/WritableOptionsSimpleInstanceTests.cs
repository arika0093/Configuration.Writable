using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableOptionsSimpleInstanceTests
{
    private readonly InMemoryFileProvider _FileProvider = new();

    [Fact]
    public void Initialize_ShouldCreateConfiguration()
    {
        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize();

        var option = _instance.GetOptions();
        var settings = option.CurrentValue;
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void GetOption_ShouldReturnWritableConfig()
    {
        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize();
        var option = _instance.GetOptions();
        option.ShouldNotBeNull();
        option.ShouldBeAssignableTo<IWritableOptions<TestSettings>>();
    }

    [Fact]
    public void GetOption_ShouldThrowIfNotInitialized()
    {
        var uninitializedInstance = new WritableOptionsSimpleInstance<TestSettings>();
        Should.Throw<InvalidOperationException>(() =>
        {
            var instance = uninitializedInstance.GetOptions();
        });
    }

    [Fact]
    public async Task Save_ShouldPersistConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "updated",
            Value = 100,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("updated");
        loadedSettings.Value.ShouldBe(100);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "async_updated",
            Value = 200,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("async_updated");
        loadedSettings.Value.ShouldBe(200);
        loadedSettings.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveWithAction_ShouldUpdateConfiguration()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var option = _instance.GetOptions();
        await option.SaveAsync(settings =>
        {
            settings.Name = "action_updated";
            settings.Value = 300;
        });

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var loadedSettings = option.CurrentValue;
        loadedSettings.Name.ShouldBe("action_updated");
        loadedSettings.Value.ShouldBe(300);
    }

    [Fact]
    public void GetConfigFilePath_ShouldReturnCorrectPath()
    {
        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize();

        var option = _instance.GetOptions();
        var path = option.GetOptionsConfiguration().ConfigFilePath;
        path.ShouldNotBeNullOrEmpty();
        path.ShouldEndWith(".json");
    }

    [Fact]
    public async Task Save_WithColonSeparatedSectionName_ShouldCreateNestedJson()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App:Settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "nested_test",
            Value = 123,
            IsEnabled = true,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "Database__Connection";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "db_test",
            Value = 456,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App:Database:Connection:Settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "deep_nested",
            Value = 789,
            IsEnabled = true,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.SectionName = "App:Config__Settings";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "mixed_separators",
            Value = 999,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(newSettings);

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var fileContent = _FileProvider.ReadAllText(testFileName);
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

        var _instance = new WritableOptionsSimpleInstance<TestSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var newSettings = new TestSettings
        {
            Name = "synccontext_test",
            Value = 888,
            IsEnabled = false,
        };

        var option = _instance.GetOptions();

        // Simulate a synchronization context that could cause deadlock
        var previousContext = SynchronizationContext.Current;
        try
        {
            var mockContext = new MockSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(mockContext);

            // This should not deadlock even with a synchronization context
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
            option.SaveAsync(newSettings).Wait();
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method

            _FileProvider.FileExists(testFileName).ShouldBeTrue();

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

    [Fact]
    public async Task OnChange_DefaultInstance_ShouldReceiveNotificationAfterSave()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"OnChangeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testFilePath = Path.Combine(testDirectory, "test.json");

        try
        {
            var _instance = new WritableOptionsSimpleInstance<TestSettings>();
            _instance.Initialize(options =>
            {
                options.FilePath = testFilePath;
            });

            var option = _instance.GetOptions();
            TestSettings? receivedValue = null;
            var callCount = 0;

            option.OnChange(value =>
            {
                receivedValue = value;
                callCount++;
            });

            // Act
            await option.SaveAsync(settings =>
            {
                settings.Name = "changed";
                settings.Value = 999;
            });

            // Wait for FileSystemWatcher to detect the change
            await Task.Delay(300);

            // Assert
            callCount.ShouldBeGreaterThanOrEqualTo(1);
            receivedValue.ShouldNotBeNull();
            receivedValue.Name.ShouldBe("changed");
            receivedValue.Value.ShouldBe(999);
        }
        finally
        {
            // Cleanup
            Thread.Sleep(100); // Wait for file handles to be released
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    [Fact]
    public async Task OnChange_WithInstanceName_ShouldReceiveNotificationWithName()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"OnChangeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testFilePath = Path.Combine(testDirectory, "test.json");

        try
        {
            var _instance = new WritableOptionsSimpleInstance<TestSettings>();
            _instance.Initialize(options =>
            {
                options.FilePath = testFilePath;
            });

            var option = _instance.GetOptions();
            var receivedNotifications = new System.Collections.Generic.List<(TestSettings value, string? name)>();

            option.OnChange((value, name) =>
            {
                receivedNotifications.Add((value, name));
            });

            // Act
            await option.SaveAsync(settings =>
            {
                settings.Name = "notification_test";
                settings.Value = 777;
            });

            // Wait for FileSystemWatcher to detect the change
            await Task.Delay(300);

            // Assert
            receivedNotifications.Count.ShouldBeGreaterThanOrEqualTo(1);
            var lastNotification = receivedNotifications[^1];
            lastNotification.value.Name.ShouldBe("notification_test");
            lastNotification.value.Value.ShouldBe(777);
        }
        finally
        {
            Thread.Sleep(100);
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    [Fact]
    public async Task OnChange_MultipleListeners_ShouldAllReceiveNotifications()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"OnChangeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testFilePath = Path.Combine(testDirectory, "test.json");

        try
        {
            var _instance = new WritableOptionsSimpleInstance<TestSettings>();
            _instance.Initialize(options =>
            {
                options.FilePath = testFilePath;
            });

            var option = _instance.GetOptions();
            var callCount1 = 0;
            var callCount2 = 0;
            var callCount3 = 0;

            option.OnChange(_ => callCount1++);
            option.OnChange((_, _) => callCount2++);
            option.OnChange(_ => callCount3++);

            // Act
            await option.SaveAsync(settings =>
            {
                settings.Name = "multi_listener";
                settings.Value = 555;
            });

            // Wait for FileSystemWatcher to detect the change
            await Task.Delay(300);

            // Assert - All listeners should be called
            callCount1.ShouldBeGreaterThanOrEqualTo(1);
            callCount2.ShouldBeGreaterThanOrEqualTo(1);
            callCount3.ShouldBeGreaterThanOrEqualTo(1);
        }
        finally
        {
            Thread.Sleep(100);
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    [Fact]
    public async Task OnChange_MultipleSaves_ShouldReceiveMultipleNotifications()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"OnChangeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testFilePath = Path.Combine(testDirectory, "test.json");

        try
        {
            var _instance = new WritableOptionsSimpleInstance<TestSettings>();
            _instance.Initialize(options =>
            {
                options.FilePath = testFilePath;
            });

            var option = _instance.GetOptions();
            var receivedValues = new System.Collections.Generic.List<TestSettings>();

            option.OnChange(value =>
            {
                receivedValues.Add(new TestSettings
                {
                    Name = value.Name,
                    Value = value.Value,
                    IsEnabled = value.IsEnabled
                });
            });

            // Act - Save multiple times with sufficient delay between saves
            // Note: Default throttle is 1000ms, so we need longer delays
            await option.SaveAsync(settings => settings.Name = "first");
            await Task.Delay(1200); // Wait for FileSystemWatcher + throttle
            await option.SaveAsync(settings => settings.Name = "second");
            await Task.Delay(1200); // Wait for FileSystemWatcher + throttle
            await option.SaveAsync(settings => settings.Name = "third");
            await Task.Delay(1200); // Wait for FileSystemWatcher + throttle

            // Assert
            receivedValues.Count.ShouldBeGreaterThanOrEqualTo(3);
            // Check that we received notifications with the expected names
            receivedValues.Any(v => v.Name == "first").ShouldBeTrue();
            receivedValues.Any(v => v.Name == "second").ShouldBeTrue();
            receivedValues.Any(v => v.Name == "third").ShouldBeTrue();
        }
        finally
        {
            Thread.Sleep(100);
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    [Fact]
    public async Task OnChange_Dispose_ShouldStopReceivingNotifications()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"OnChangeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testFilePath = Path.Combine(testDirectory, "test.json");

        try
        {
            var _instance = new WritableOptionsSimpleInstance<TestSettings>();
            _instance.Initialize(options =>
            {
                options.FilePath = testFilePath;
            });

            var option = _instance.GetOptions();
            var callCount = 0;

            var subscription = option.OnChange(_ => callCount++);

            // Act - Save, then dispose, then save again
            await option.SaveAsync(settings => settings.Name = "before_dispose");
            await Task.Delay(300); // Wait for FileSystemWatcher
            var countBeforeDispose = callCount;
            countBeforeDispose.ShouldBeGreaterThanOrEqualTo(1);

            subscription?.Dispose();

            await option.SaveAsync(settings => settings.Name = "after_dispose");
            await Task.Delay(300); // Wait for FileSystemWatcher

            // Assert - Should not have received new notifications after dispose
            callCount.ShouldBe(countBeforeDispose);
        }
        finally
        {
            Thread.Sleep(100);
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    [Fact]
    public async Task OnChange_CurrentValue_ShouldReflectLatestChanges()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"OnChangeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testFilePath = Path.Combine(testDirectory, "test.json");

        try
        {
            var _instance = new WritableOptionsSimpleInstance<TestSettings>();
            _instance.Initialize(options =>
            {
                options.FilePath = testFilePath;
            });

            var option = _instance.GetOptions();
            var initialValue = option.CurrentValue.Name;
            initialValue.ShouldBe("default");

            TestSettings? notifiedValue = null;
            option.OnChange(value => notifiedValue = value);

            // Act
            await option.SaveAsync(settings =>
            {
                settings.Name = "updated_value";
                settings.Value = 321;
            });

            // Wait for FileSystemWatcher to detect the change
            await Task.Delay(300);

            // Assert - Both CurrentValue and notification should have latest value
            option.CurrentValue.Name.ShouldBe("updated_value");
            option.CurrentValue.Value.ShouldBe(321);
            notifiedValue.ShouldNotBeNull();
            notifiedValue.Name.ShouldBe("updated_value");
            notifiedValue.Value.ShouldBe(321);
        }
        finally
        {
            Thread.Sleep(100);
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}

file class TestSettings
{
    public string Name { get; set; } = "default";
    public int Value { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
}

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
