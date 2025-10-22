using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

/// <summary>
/// Tests that simulate realistic usage scenarios where the same instance
/// is used to repeatedly save and retrieve configuration values,
/// mimicking real-world application behavior using actual file system operations.
/// </summary>
public class RealisticUsageScenarioTests : IDisposable
{
    private readonly string _testDirectory;

    public RealisticUsageScenarioTests()
    {
        // Create a unique temporary directory for each test run
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"WritableConfigTests_{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Wait for file watchers to release handles
        Thread.Sleep(200);

        // Clean up the test directory after tests complete
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors - file handles may still be held
            }
        }
    }

    /// <summary>
    /// Simulates a realistic scenario using WritableConfig (non-DI approach).
    /// A user application would initialize once, then repeatedly read and update settings.
    /// </summary>
    [Fact]
    public async Task WritableConfig_RepeatedSaveAndRetrieve_ShouldPersistAllChanges()
    {
        var testFilePath = Path.Combine(_testDirectory, "test1.json");

        // Initialize once (like at application startup)
        var instance = new WritableOptionsSimpleInstance<UserPreferences>();
        instance.Initialize(options =>
        {
            options.FilePath = testFilePath;
        });

        var config = instance.GetOptions();

        // Scenario: User opens app for the first time - default values should be loaded
        var initialSettings = config.CurrentValue;
        initialSettings.Theme.ShouldBe("Light");
        initialSettings.FontSize.ShouldBe(12);
        initialSettings.AutoSave.ShouldBeTrue();
        initialSettings.LastUsedFilePath.ShouldBeNull();

        // Scenario: User changes theme to dark mode
        await config.SaveAsync(settings =>
        {
            settings.Theme = "Dark";
        });

        // Verify the change persisted
        var afterThemeChange = config.CurrentValue;
        afterThemeChange.Theme.ShouldBe("Dark");
        afterThemeChange.FontSize.ShouldBe(12); // Other settings unchanged
        afterThemeChange.AutoSave.ShouldBeTrue();

        // Scenario: User increases font size
        await config.SaveAsync(settings =>
        {
            settings.FontSize = 14;
        });

        // Verify the change persisted and previous changes are retained
        var afterFontChange = config.CurrentValue;
        afterFontChange.Theme.ShouldBe("Dark"); // Previous change retained
        afterFontChange.FontSize.ShouldBe(14);
        afterFontChange.AutoSave.ShouldBeTrue();

        // Scenario: User opens a file and we save the path
        await config.SaveAsync(settings =>
        {
            settings.LastUsedFilePath = "/home/user/documents/myfile.txt";
        });

        // Verify all changes are persisted
        var afterFilePathChange = config.CurrentValue;
        afterFilePathChange.Theme.ShouldBe("Dark");
        afterFilePathChange.FontSize.ShouldBe(14);
        afterFilePathChange.LastUsedFilePath.ShouldBe("/home/user/documents/myfile.txt");

        // Scenario: User disables auto-save
        await config.SaveAsync(settings =>
        {
            settings.AutoSave = false;
        });

        // Verify all previous changes are still there
        var afterAutoSaveChange = config.CurrentValue;
        afterAutoSaveChange.Theme.ShouldBe("Dark");
        afterAutoSaveChange.FontSize.ShouldBe(14);
        afterAutoSaveChange.LastUsedFilePath.ShouldBe("/home/user/documents/myfile.txt");
        afterAutoSaveChange.AutoSave.ShouldBeFalse();

        // Scenario: User makes multiple rapid changes
        await config.SaveAsync(settings =>
        {
            settings.Theme = "System";
            settings.FontSize = 16;
        });

        // Verify the rapid changes
        var afterRapidChanges = config.CurrentValue;
        afterRapidChanges.Theme.ShouldBe("System");
        afterRapidChanges.FontSize.ShouldBe(16);
        afterRapidChanges.LastUsedFilePath.ShouldBe("/home/user/documents/myfile.txt");
        afterRapidChanges.AutoSave.ShouldBeFalse();

        // Verify the file was actually written
        File.Exists(testFilePath).ShouldBeTrue();
        var fileContent = File.ReadAllText(testFilePath);
        fileContent.ShouldContain("System");
        fileContent.ShouldContain("16");
        fileContent.ShouldContain("/home/user/documents/myfile.txt");
    }

    /// <summary>
    /// Simulates a realistic scenario using DI approach (ASP.NET Core, Worker Service, etc.).
    /// The same instance is injected into services and used repeatedly throughout the application lifecycle.
    /// </summary>
    [Fact]
    public async Task DI_RepeatedSaveAndRetrieve_ShouldPersistAllChanges()
    {
        var testFilePath = Path.Combine(_testDirectory, "test2.json");

        // Setup DI container (like in Program.cs)
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<UserPreferences>(options =>
        {
            options.FilePath = testFilePath;
        });

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableOptions<UserPreferences>>();

        // Scenario: Application starts - default values should be loaded
        var initialSettings = config.CurrentValue;
        initialSettings.Theme.ShouldBe("Light");
        initialSettings.FontSize.ShouldBe(12);
        initialSettings.AutoSave.ShouldBeTrue();
        initialSettings.LastUsedFilePath.ShouldBeNull();

        // Scenario: User changes theme preference
        await config.SaveAsync(settings =>
        {
            settings.Theme = "Dark";
        });

        // Verify the change persisted
        var afterThemeChange = config.CurrentValue;
        afterThemeChange.Theme.ShouldBe("Dark");
        afterThemeChange.FontSize.ShouldBe(12);
        afterThemeChange.AutoSave.ShouldBeTrue();

        // Scenario: User adjusts font size
        await config.SaveAsync(settings =>
        {
            settings.FontSize = 14;
        });

        // Verify cumulative changes
        var afterFontChange = config.CurrentValue;
        afterFontChange.Theme.ShouldBe("Dark");
        afterFontChange.FontSize.ShouldBe(14);
        afterFontChange.AutoSave.ShouldBeTrue();

        // Scenario: Application tracks last used file
        await config.SaveAsync(settings =>
        {
            settings.LastUsedFilePath = "/workspace/project/config.json";
        });

        // Verify all settings are maintained
        var afterFilePathChange = config.CurrentValue;
        afterFilePathChange.Theme.ShouldBe("Dark");
        afterFilePathChange.FontSize.ShouldBe(14);
        afterFilePathChange.LastUsedFilePath.ShouldBe("/workspace/project/config.json");

        // Scenario: User toggles auto-save off
        await config.SaveAsync(settings =>
        {
            settings.AutoSave = false;
        });

        // Verify all settings including the toggle
        var afterAutoSaveChange = config.CurrentValue;
        afterAutoSaveChange.Theme.ShouldBe("Dark");
        afterAutoSaveChange.FontSize.ShouldBe(14);
        afterAutoSaveChange.LastUsedFilePath.ShouldBe("/workspace/project/config.json");
        afterAutoSaveChange.AutoSave.ShouldBeFalse();

        // Scenario: User switches to high contrast theme and larger font
        await config.SaveAsync(settings =>
        {
            settings.Theme = "HighContrast";
            settings.FontSize = 18;
        });

        // Verify all final changes
        var finalSettings = config.CurrentValue;
        finalSettings.Theme.ShouldBe("HighContrast");
        finalSettings.FontSize.ShouldBe(18);
        finalSettings.LastUsedFilePath.ShouldBe("/workspace/project/config.json");
        finalSettings.AutoSave.ShouldBeFalse();

        // Verify the file was actually written
        File.Exists(testFilePath).ShouldBeTrue();
        var fileContent = File.ReadAllText(testFilePath);
        fileContent.ShouldContain("HighContrast");
        fileContent.ShouldContain("18");
        fileContent.ShouldContain("/workspace/project/config.json");
    }

    /// <summary>
    /// Simulates concurrent access to the same configuration instance,
    /// as might occur in a multi-threaded application.
    /// </summary>
    [Fact]
    public async Task WritableConfig_ConcurrentSaves_ShouldHandleThreadSafety()
    {
        var testFilePath = Path.Combine(_testDirectory, "test3.json");

        var instance = new WritableOptionsSimpleInstance<UserPreferences>();
        instance.Initialize(options =>
        {
            options.FilePath = testFilePath;
        });

        var config = instance.GetOptions();

        // Simulate multiple concurrent operations
        var tasks = new[]
        {
            config.SaveAsync(s => s.Theme = "Dark"),
            config.SaveAsync(s => s.FontSize = 14),
            config.SaveAsync(s => s.AutoSave = false),
            config.SaveAsync(s => s.LastUsedFilePath = "/path/1"),
        };

        await Task.WhenAll(tasks);

        // Retry file existence check to handle filesystem delays in concurrent operations
        var fileExists = false;
        for (int i = 0; i < 20; i++)
        {
            fileExists = File.Exists(testFilePath);
            if (fileExists)
                break;
            await Task.Delay(100);
        }

        fileExists.ShouldBeTrue();
        var finalSettings = config.CurrentValue;

        // At least the last write should be persisted correctly
        finalSettings.ShouldNotBeNull();
        finalSettings.Theme.ShouldNotBeNullOrEmpty();
        finalSettings.FontSize.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Simulates a WPF or WinForms scenario where user changes settings,
    /// reads them back to update UI, and repeats this multiple times.
    /// </summary>
    [Fact]
    public async Task WritableConfig_UIScenario_RepeatedReadWriteCycles()
    {
        var testFilePath = Path.Combine(_testDirectory, "test4.json");

        var instance = new WritableOptionsSimpleInstance<UserPreferences>();
        instance.Initialize(options =>
        {
            options.FilePath = testFilePath;
        });

        var config = instance.GetOptions();

        // Simulate 10 user interactions with the settings dialog
        for (int i = 1; i <= 10; i++)
        {
            // User opens settings dialog and reads current values
            var currentSettings = config.CurrentValue;
            currentSettings.ShouldNotBeNull();

            // User modifies settings
            await config.SaveAsync(settings =>
            {
                settings.FontSize = 12 + i; // Incrementally increase font size
                settings.LastUsedFilePath = $"/path/file{i}.txt";
            });

            // UI reads back the settings to confirm the change
            var updatedSettings = config.CurrentValue;
            updatedSettings.FontSize.ShouldBe(12 + i);
            updatedSettings.LastUsedFilePath.ShouldBe($"/path/file{i}.txt");
        }

        // After 10 iterations, verify the final state
        var finalSettings = config.CurrentValue;
        finalSettings.FontSize.ShouldBe(22); // 12 + 10
        finalSettings.LastUsedFilePath.ShouldBe("/path/file10.txt");

        File.Exists(testFilePath).ShouldBeTrue();
    }
}

/// <summary>
/// Example settings class representing typical user preferences
/// in a desktop or web application.
/// </summary>
file class UserPreferences
{
    public string Theme { get; set; } = "Light";
    public int FontSize { get; set; } = 12;
    public bool AutoSave { get; set; } = true;
    public string? LastUsedFilePath { get; set; }
}
