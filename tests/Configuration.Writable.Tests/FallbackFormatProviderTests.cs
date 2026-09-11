using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Internal;
using Configuration.Writable.Migration;
using Configuration.Writable.Options;
using Shouldly;

namespace Configuration.Writable.Tests;

public class FallbackFormatProviderTests
{
    [Test]
    public async Task FallbackFormat_ShouldLoadMigrateAndPromoteToCanonicalFormat()
    {
        var fileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = "fallback-settings",
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"Version":1,"Name":"legacy"}""")
        );

        var result = options.FormatProvider.LoadWithMigration(options);

        result.Names.ShouldBe(["legacy"]);
        fileProvider.FileExists(options.ConfigFilePath).ShouldBeTrue();
        fileProvider.ReadAllText(options.ConfigFilePath).ShouldContain("Names");
        fileProvider.FileExists(fallbackPath).ShouldBeTrue();
    }

    [Test]
    public async Task FallbackFormat_ShouldBackUpFallbackBeforePromotingToCanonicalFormat()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new CommonFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = testFile.FilePath,
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"Version\":1,\"Name\":\"legacy\"}")
        );

        options.FormatProvider.LoadWithMigration(options).Names.ShouldBe(["legacy"]);

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
        var fileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "fallback-settings",
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.AddFallbackFormatProvider(
            new LegacyJsonFormatProvider("legacy1"),
            new LegacyJsonFormatProvider("legacy2")
        );

        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy2");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"Name":"second fallback"}""")
        );

        var result = options.FormatProvider.LoadWithMigration(options);

        result.Name.ShouldBe("second fallback");
    }

    [Test]
    public async Task FallbackFormat_ShouldSaveSectionToFallbackWithoutDroppingSiblings()
    {
        var fileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "sectioned-settings",
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
            SectionName = "First",
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes(
                "{\"First\":{\"Name\":\"before\"},\"Second\":{\"Name\":\"sibling\"}}"
            )
        );

        await options.FormatProvider.SaveAsync(
            new MigrationSupportTests.SettingsWithoutVersion { Name = "after" },
            options
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
        var fileProvider = new CommonFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
            SectionName = "First",
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"before\"}}")
        );

        var before = ConfigurationFileFingerprint.Capture(options);
        File.WriteAllText(fallbackPath, "{\"First\":{\"Name\":\"after\"}}");
        var after = ConfigurationFileFingerprint.Capture(options);

        before.ShouldNotBe(after);
    }

    [Test]
    public async Task FallbackFormat_ShouldKeepFallbackDocumentForSectionedConfigurations()
    {
        var fileProvider = new InMemoryFileProvider();
        var firstBuilder =
            new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
            {
                FilePath = "sectioned-settings",
                FileProvider = fileProvider,
                FormatProvider = new JsonFormatProvider(),
                SectionName = "First",
            };
        firstBuilder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));

        var firstOptions = firstBuilder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(firstOptions.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"First":{"Name":"first"},"Second":{"Name":"second"}}""")
        );

        var first = firstOptions.FormatProvider.LoadWithMigration(firstOptions);

        first.Name.ShouldBe("first");
        fileProvider.FileExists(firstOptions.ConfigFilePath).ShouldBeFalse();

        var secondBuilder =
            new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
            {
                FilePath = "sectioned-settings",
                FileProvider = fileProvider,
                FormatProvider = new JsonFormatProvider(),
                SectionName = "Second",
            };
        secondBuilder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var secondOptions = secondBuilder.BuildOptions("");

        secondOptions.FormatProvider.LoadWithMigration(secondOptions).Name.ShouldBe("second");
    }

    [Test]
    public void FallbackFormat_ShouldRequireMetadataSupportForVersionedOptions()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FormatProvider = new JsonFormatProvider(),
        };

        builder.AddFallbackFormatProvider(new MetadataUnsupportedFormatProvider());

        Should.Throw<InvalidOperationException>(() => builder.BuildOptions(""));
    }

    [Test]
    public void AddFallbackFormatProvider_ShouldRejectDuplicateExtension()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FormatProvider = new JsonFormatProvider(),
        };

        Should.Throw<InvalidOperationException>(() =>
            builder.AddFallbackFormatProvider(new JsonFormatProvider())
        );
    }

    [Test]
    public async Task FallbackFormat_ShouldRestoreCanonicalBackupBeforeSelectingFallback()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new CommonFileProvider { BackupDirectory = "/" };
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
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

        var result = options.FormatProvider.LoadWithMigration(options);

        result.Name.ShouldBe("canonical backup");
    }

    [Test]
    public async Task FallbackFormat_ShouldRecoverSelectedFallbackBeforeReadingMetadata()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new CommonFileProvider { BackupDirectory = "/" };
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = testFile.FilePath,
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");

        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"Version\":1,\"Name\":\"recoverable fallback\"}")
        );
        await fileProvider.SaveToFileAsync(fallbackPath, Encoding.UTF8.GetBytes("{"));

        var result = options.FormatProvider.LoadWithMigration(options);

        result.Names.ShouldBe(["recoverable fallback"]);
    }

    [Test]
    public async Task FallbackFormat_ShouldWatchSelectedFallbackForChanges()
    {
        using var testFile = new TemporaryFile();
        var fileProvider = new CommonFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
            SectionName = "First",
            OnChangeDebounce = TimeSpan.Zero,
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"before\"}}")
        );

        var registry =
            new WritableOptionsConfigRegistryImpl<MigrationSupportTests.SettingsWithoutVersion>([
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
        var fileProvider = new CommonFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = testFile.FilePath,
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
            SectionName = "First",
            OnChangeDebounce = TimeSpan.Zero,
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("legacy"));
        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("{\"First\":{\"Name\":\"fallback\"}}")
        );

        var registry =
            new WritableOptionsConfigRegistryImpl<MigrationSupportTests.SettingsWithoutVersion>([
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

    private sealed class LegacyJsonFormatProvider(string extension) : JsonFormatProvider
    {
        public override string FileExtension => extension;
    }

    private sealed class MetadataUnsupportedFormatProvider : FormatProviderBase
    {
        public override string FileExtension => "legacy";

        public override ValueTask<object> LoadConfigurationAsync(
            Type type,
            PipeReader reader,
            List<string> sectionNameParts,
            CancellationToken cancellationToken = default
        ) =>
            new JsonFormatProvider().LoadConfigurationAsync(
                type,
                reader,
                sectionNameParts,
                cancellationToken
            );

        public override Task SaveAsync<T>(
            T config,
            IWritableOptionsConfiguration options,
            CancellationToken cancellationToken = default
        ) => new JsonFormatProvider().SaveAsync(config, options, cancellationToken);
    }
}
