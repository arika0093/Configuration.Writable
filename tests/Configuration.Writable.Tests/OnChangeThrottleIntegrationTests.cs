using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.Tests.Utility;

namespace Configuration.Writable.Tests;

/// <summary>
/// Integration tests for OnChange debounce functionality using actual file system.
/// </summary>
public partial class OnChangeDebounceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public OnChangeDebounceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DebounceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Wait for file watchers to release handles
        Thread.Sleep(300);

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [OptionsModel]
    public partial class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 0;
    }

    [Fact]
    public async Task OnChangeDebounce_RapidFileChanges_ShouldDebounceNotifications()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "debounce_test.json");
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFilePath;
            options.OnChangeDebounce = TimeSpan.FromMilliseconds(500); // 500ms debounce
        });

        var config = instance.GetOptions();
        var changeCount = 0;
        var receivedValues = new System.Collections.Generic.List<TestSettings>();

        // Subscribe to change notifications
        config.OnChange(
            (value, name) =>
            {
                Interlocked.Increment(ref changeCount);
                lock (receivedValues)
                {
                    receivedValues.Add(new TestSettings { Name = value.Name, Value = value.Value });
                }
            }
        );

        // Save initial value
        await config.SaveAsync(s =>
        {
            s.Name = "initial";
            s.Value = 0;
        });
        // Wait reliably for the initial save to deliver its notification so the
        // count below represents only the rapid-change phase.
        await FileWatcherTestHelper.WaitForConditionAsync(() =>
        {
            lock (receivedValues)
            {
                return receivedValues.Count >= 1;
            }
        });

        var initialChangeCount = changeCount;

        // Act - Rapidly modify the file externally (simulating external editor changes)
        for (int i = 1; i <= 5; i++)
        {
            var content = System.Text.Json.JsonSerializer.Serialize(
                new TestSettings { Name = $"change{i}", Value = i }
            );
            File.WriteAllText(testFilePath, content);
            Thread.Sleep(50); // Small delay between writes
        }

        // Wait reliably for the debounce period to elapse and the final change to arrive.
        await FileWatcherTestHelper.WaitForConditionAsync(
            () =>
            {
                lock (receivedValues)
                {
                    return receivedValues.Count > 0
                        && receivedValues[^1].Name == "change5"
                        && receivedValues[^1].Value == 5;
                }
            },
            timeout: TimeSpan.FromSeconds(5)
        );

        // Assert - Debouncing coalesces rapid changes and delivers the final file contents.
        var changesAfterRapidWrites = changeCount - initialChangeCount;
        changesAfterRapidWrites.ShouldBeLessThanOrEqualTo(2);
        lock (receivedValues)
        {
            receivedValues[^1].Name.ShouldBe("change5");
            receivedValues[^1].Value.ShouldBe(5);
        }
    }
}
