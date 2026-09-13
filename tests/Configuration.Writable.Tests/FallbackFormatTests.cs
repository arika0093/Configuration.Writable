using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.Internal;
using Configuration.Writable.Migration;
using Configuration.Writable.Options;
using Configuration.Writable.State;
using Shouldly;

namespace Configuration.Writable.Tests;

public class FallbackFormatTests
{
    [Test]
    public async Task FallbackFormat_ShouldLoadMigrateAndPromoteToCanonicalFormat()
    {
        var fileProvider = new InMemoryFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = "fallback-settings",
            FileBackend = fileProvider,
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"Version":1,"Name":"legacy"}""")
        );

        var result = ReadValue(options);

        result.Names.ShouldBe(["legacy"]);
        fileProvider.FileExists(options.ConfigFilePath).ShouldBeTrue();
        fileProvider.ReadAllText(options.ConfigFilePath).ShouldContain("Names");
        fileProvider.FileExists(fallbackPath).ShouldBeTrue();
    }

    [Test]
    public async Task FallbackFormat_ShouldBackUpFallbackBeforePromotingToCanonicalFormat()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new PhysicalFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = testFile.FilePath,
            FileBackend = fileProvider,
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"Version\":1,\"Name\":\"legacy\"}")
        );

        ReadValue(options).Names.ShouldBe(["legacy"]);

        var backupDirectory = Path.Combine(
            Path.GetDirectoryName(fallbackPath)!,
            Path.DirectorySeparatorChar == '\\' ? "backup" : ".backup"
        );
        Directory.GetFiles(backupDirectory, "*.legacy.bak").Length.ShouldBe(1);
        File.Exists(fallbackPath).ShouldBeFalse();
    }

    [Test]
    public async Task FallbackFormat_ShouldTryProvidersInRegistrationOrder()
    {
        var fileProvider = new InMemoryFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "fallback-settings",
            FileBackend = fileProvider,
        };
        builder.AddFallbackFormat(
            new LegacyJsonFileOptions("legacy1"),
            new LegacyJsonFileOptions("legacy2")
        );

        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy2");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"Name":"second fallback"}""")
        );

        var result = ReadValue(options);

        result.Name.ShouldBe("second fallback");
    }

    [Test]
    public async Task FallbackFormat_ShouldSaveSectionToFallbackWithoutDroppingSiblings()
    {
        var fileProvider = new InMemoryFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "sectioned-settings",
            FileBackend = fileProvider,
            SectionName = "First",
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes(
                "{\"First\":{\"Name\":\"before\"},\"Second\":{\"Name\":\"sibling\"}}"
            )
        );

        await SaveValue(
            options,
            new MigrationSupportTests.SettingsWithoutVersion { Name = "after" }
        );

        fileProvider.FileExists(options.ConfigFilePath).ShouldBeFalse();
        var savedFallback = fileProvider.ReadAllText(fallbackPath);
        savedFallback.ShouldContain("\"after\"");
        savedFallback.ShouldContain("\"sibling\"");
    }

    [Test]
    public async Task FallbackFormat_ShouldFingerprintSelectedFallbackFile()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new PhysicalFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileBackend = fileProvider,
            SectionName = "First",
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"before\"}}")
        );

        var before = ConfigurationFileFingerprint.Capture(options.GetSelectedFilePath(), options.FileBackend);
        File.WriteAllText(fallbackPath, "{\"First\":{\"Name\":\"after\"}}");
        var after = ConfigurationFileFingerprint.Capture(options.GetSelectedFilePath(), options.FileBackend);

        before.ShouldNotBe(after);
    }

    [Test]
    public async Task FallbackFormat_ShouldKeepFallbackDocumentForSectionedConfigurations()
    {
        var fileProvider = new InMemoryFileBackend();
        var firstBuilder =
            new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
            {
                FilePath = "sectioned-settings",
                FileBackend = fileProvider,
                SectionName = "First",
            };
        firstBuilder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));

        var firstOptions = firstBuilder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(firstOptions.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"First":{"Name":"first"},"Second":{"Name":"second"}}""")
        );

        var first = ReadValue(firstOptions);

        first.Name.ShouldBe("first");
        fileProvider.FileExists(firstOptions.ConfigFilePath).ShouldBeFalse();

        var secondBuilder =
            new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
            {
                FilePath = "sectioned-settings",
                FileBackend = fileProvider,
                SectionName = "Second",
            };
        secondBuilder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var secondOptions = secondBuilder.BuildOptions("");

        ReadValue(secondOptions).Name.ShouldBe("second");
    }

    [Test]
    public void AddFallbackFormat_ShouldRejectDuplicateExtension()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
        };

        Should.Throw<InvalidOperationException>(() =>
            builder.AddFallbackFormat(new JsonFileOptions())
        );
    }

    [Test]
    public async Task FallbackFormat_ShouldRestoreCanonicalBackupBeforeSelectingFallback()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new PhysicalFileBackend { BackupDirectory = "/" };
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileBackend = fileProvider,
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");

        await fileProvider.SaveToFileAsync(
            options.ConfigFilePath,
            Encoding.UTF8.GetBytes("{\"Name\":\"canonical backup\"}")
        );
        await fileProvider.SaveToFileAsync(
            options.ConfigFilePath,
            Encoding.UTF8.GetBytes("{\"Name\":\"current canonical\"}")
        );
        File.Delete(options.ConfigFilePath);

        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"Name\":\"stale fallback\"}")
        );

        var result = ReadValue(options);

        result.Name.ShouldBe("canonical backup");
    }

    [Test]
    public async Task FallbackFormat_ShouldRecoverSelectedFallbackBeforeReadingMetadata()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new PhysicalFileBackend { BackupDirectory = "/" };
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = testFile.FilePath,
            FileBackend = fileProvider,
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");

        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"Version\":1,\"Name\":\"recoverable fallback\"}")
        );
        await fileProvider.SaveToFileAsync(fallbackPath, Encoding.UTF8.GetBytes("{"));

        var result = ReadValue(options);

        result.Names.ShouldBe(["recoverable fallback"]);
    }

    [Test]
    public async Task FallbackFormat_ShouldWatchSelectedFallbackForChanges()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new PhysicalFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileBackend = fileProvider,
            SectionName = "First",
            OnChangeDebounce = TimeSpan.Zero,
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"before\"}}")
        );

        var registry =
            new WritableOptionsRegistry<MigrationSupportTests.SettingsWithoutVersion>([
                options,
            ]);
        using var monitor = new OptionsMonitorImpl<MigrationSupportTests.SettingsWithoutVersion>(
            registry
        );
        MigrationSupportTests.SettingsWithoutVersion? changed = null;
        using var registration = monitor.OnChange((value, _) => changed = value);

        File.WriteAllText(fallbackPath, "{\"First\":{\"Name\":\"after\"}}");

        var changedResult = await Utility.FileWatcherTestHelper.WaitForNonNullAsync(() => changed);
        changedResult.ShouldNotBeNull();
        changedResult.Name.ShouldBe("after");
    }

    [Test]
    public async Task FallbackFormat_ShouldRebindWatcherWhenCanonicalFileAppears()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new PhysicalFileBackend();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileBackend = fileProvider,
            SectionName = "First",
            OnChangeDebounce = TimeSpan.Zero,
        };
        builder.AddFallbackFormat(new LegacyJsonFileOptions("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"fallback\"}}")
        );

        var registry =
            new WritableOptionsRegistry<MigrationSupportTests.SettingsWithoutVersion>([
                options,
            ]);
        using var monitor = new OptionsMonitorImpl<MigrationSupportTests.SettingsWithoutVersion>(
            registry
        );
        MigrationSupportTests.SettingsWithoutVersion? changed = null;
        using var registration = monitor.OnChange((value, _) => changed = value);

        await fileProvider.SaveToFileAsync(
            options.ConfigFilePath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"canonical\"}}")
        );

        var canonicalResult = await Utility.FileWatcherTestHelper.WaitForNonNullAsync(() =>
            changed
        );
        canonicalResult.ShouldNotBeNull();
        canonicalResult.Name.ShouldBe("canonical");

        changed = null;
        File.WriteAllText(fallbackPath, "{\"First\":{\"Name\":\"ignored\"}}");
        (
            await Utility.FileWatcherTestHelper.WaitForConditionAsync(
                () => changed is not null,
                TimeSpan.FromMilliseconds(500)
            )
        ).ShouldBeFalse();
    }

    private sealed class LegacyJsonFileOptions(string extension) : JsonFileOptions
    {
        public override string FileExtension => extension;
    }

    private static T ReadValue<T>(WritableOptionsConfiguration<T> options)
        where T : class, new()
    {
        var result = options
            .CreateStateSource()
            .ReadAsync()
            .AsTask()
            .GetAwaiter()
            .GetResult();
        result.Status.ShouldBe(StateReadStatus.Success);
        return result.Value!;
    }

    private static Task SaveValue<T>(WritableOptionsConfiguration<T> options, T value)
        where T : class, new() =>
        options.CreateStateSource().WriteAsync(new StateWriteRequest<T>(value, null)).AsTask();
}
