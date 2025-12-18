using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.Tests;

/// <summary>
/// Integration tests for OnChange throttle functionality using actual file system.
/// </summary>
public class OnChangeThrottleIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public OnChangeThrottleIntegrationTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ThrottleTests_{Guid.NewGuid():N}"
        );
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

    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 0;
    }

    [Fact]
    public async Task OnChangeThrottle_RapidFileChanges_ShouldThrottleNotifications()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "throttle_test.json");
        var instance = new WritableOptionsSimpleInstance<TestSettings>();

        instance.Initialize(options =>
        {
            options.FilePath = testFilePath;
            options.OnChangeThrottleMs = 500; // 500ms throttle
        });

        var config = instance.GetOptions();
        var changeCount = 0;
        var receivedValues = new System.Collections.Generic.List<TestSettings>();

        // Subscribe to change notifications
        config.OnChange((value, name) =>
        {
            Interlocked.Increment(ref changeCount);
            lock (receivedValues)
            {
                receivedValues.Add(new TestSettings { Name = value.Name, Value = value.Value });
            }
        });

        // Save initial value
        await config.SaveAsync(s => { s.Name = "initial"; s.Value = 0; });
        Thread.Sleep(100); // Allow file watcher to initialize

        var initialChangeCount = changeCount;

        // Act - Rapidly modify the file externally (simulating external editor changes)
        for (int i = 1; i <= 5; i++)
        {
            var content = System.Text.Json.JsonSerializer.Serialize(new TestSettings
            {
                Name = $"change{i}",
                Value = i
            });
            File.WriteAllText(testFilePath, content);
            Thread.Sleep(50); // Small delay between writes
        }

        // Wait for throttle period + buffer
        Thread.Sleep(700);

        // Assert - Should have received significantly fewer changes than the number of writes
        // due to throttling (exact count may vary due to timing, but should be <= 2)
        var changesAfterRapidWrites = changeCount - initialChangeCount;
        changesAfterRapidWrites.ShouldBeLessThanOrEqualTo(2);
    }
}
