using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Options;
using Configuration.Writable.State;
using Configuration.Writable.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.Tests;

public partial class GroupedWritableOptionsTests
{
    [OptionsModel]
    public partial class FirstSettings
    {
        public string Value { get; set; } = "first";
    }

    [OptionsModel]
    public partial class SecondSettings
    {
        public string Value { get; set; } = "second";
    }

    [OptionsModel]
    public partial class NamedSettings
    {
        public string Value { get; set; } = "named";
    }

    [OptionsModel]
    public partial class ReinitializedSettings
    {
        public string Value { get; set; } = "retained";
    }

    [Test]
    public void GroupedRegistration_AppliesSharedConfigurationAfterRecipesAreCollected()
    {
        var provider = new InMemoryFileBackend();
        var services = new ServiceCollection();

        services.AddWritableOptions(options =>
        {
            options.Add<FirstSettings>(x => x.AddFilePath("first.json"));
            options.Add<SecondSettings>(x => x.AddFilePath("second.json"));
            options.UseCustomDirectory("grouped");
            options.UseInMemoryBackend(provider);
        });

        using var serviceProvider = services.BuildServiceProvider();
        var first = serviceProvider
            .GetRequiredService<WritableOptionsRegistry<FirstSettings>>()
            .Get(string.Empty);
        var second = serviceProvider
            .GetRequiredService<WritableOptionsRegistry<SecondSettings>>()
            .Get(string.Empty);

        first.FileBackend.ShouldBeSameAs(provider);
        second.FileBackend.ShouldBeSameAs(provider);
        first.ConfigFilePath.ShouldBe(Path.GetFullPath(Path.Combine("grouped", "first.json")));
        second.ConfigFilePath.ShouldBe(Path.GetFullPath(Path.Combine("grouped", "second.json")));
    }

    [Test]
    public void GroupedRegistration_TypeConfigurationOverridesSharedConfiguration()
    {
        var provider = new InMemoryFileBackend();
        var services = new ServiceCollection();
        services.AddWritableOptions(options =>
        {
            options.UseInMemoryBackend(provider);
            options.UseFile("shared.json");
            options.SectionName = "Shared";
            options.Add<FirstSettings>(x =>
            {
                x.UseFile("specific.json");
                x.SectionName = "Specific";
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider
            .GetRequiredService<WritableOptionsRegistry<FirstSettings>>()
            .Get(string.Empty);
        configuration.ConfigFilePath.ShouldBe(Path.GetFullPath("specific.json"));
        configuration.SectionNameParts.ShouldBe(["Specific"]);
    }

    [Test]
    public void GroupedRegistration_ClonesFallbackFormatsPerType()
    {
        var provider = new InMemoryFileBackend();
        var services = new ServiceCollection();
        services.AddWritableOptions(options =>
        {
            options.UseInMemoryBackend(provider);
            options.UseFile("shared.json");
            options.Add<FirstSettings>(builder =>
                builder.AddFallbackFormat(new LegacyJsonFileOptions("first"))
            );
            options.Add<SecondSettings>(builder =>
                builder.AddFallbackFormat(new LegacyJsonFileOptions("second"))
            );
        });

        using var serviceProvider = services.BuildServiceProvider();
        var first = serviceProvider
            .GetRequiredService<WritableOptionsRegistry<FirstSettings>>()
            .Get(string.Empty);
        var second = serviceProvider
            .GetRequiredService<WritableOptionsRegistry<SecondSettings>>()
            .Get(string.Empty);

        first.FallbackFormats.Count.ShouldBe(1);
        second.FallbackFormats.Count.ShouldBe(1);
        first.FallbackFormats.ShouldNotBeSameAs(second.FallbackFormats);
        first.FallbackFormats[0].FileExtension.ShouldBe("first");
        second.FallbackFormats[0].FileExtension.ShouldBe("second");
    }

    [Test]
    public void StaticGroupedInitialization_RetainsEveryNamedRegistration()
    {
        var provider = new InMemoryFileBackend();
        WritableOptions.Initialize(options =>
        {
            options.UseInMemoryBackend(provider);
            options.Add<NamedSettings>("First", conf => conf.UseFile("first.json"));
            options.Add<NamedSettings>("Second", conf => conf.UseFile("second.json"));
        });

        var writableOptions =
            (IWritableOptionsMonitor<NamedSettings>)WritableOptions.GetOptions<NamedSettings>();
        writableOptions.Get("First").ShouldNotBeNull();
        writableOptions.Get("Second").ShouldNotBeNull();
    }

    [Test]
    public void SimpleInstance_ReinitializationFailureRetainsPreviousConfiguration()
    {
        var provider = new InMemoryFileBackend();
        var instance = new WritableOptionsSimpleInstance<ReinitializedSettings>();
        instance.Initialize(options =>
        {
            options.UseInMemoryBackend(provider);
            options.UseFile("existing.json");
        });

        Should.Throw<InvalidOperationException>(() =>
            instance.Initialize(options =>
            {
                options.FileBackend = new RejectingFileBackend();
                options.UseFile("replacement.json");
            })
        );

        instance
            .GetOptions()
            .ConfigurationInfo.WritePath.ShouldBe(Path.GetFullPath("existing.json"));
    }

    [Test]
    public void StaticGroupedInitialization_FailureRetainsPreviousConfiguration()
    {
        var provider = new InMemoryFileBackend();
        WritableOptions.Initialize<ReinitializedSettings>(options =>
        {
            options.UseInMemoryBackend(provider);
            options.UseFile("existing.json");
        });

        Should.Throw<InvalidOperationException>(() =>
            WritableOptions.Initialize(options =>
            {
                options.FileBackend = new RejectingFileBackend();
                options.Add<ReinitializedSettings>(conf => conf.UseFile("replacement.json"));
            })
        );

        WritableOptions
            .GetOptions<ReinitializedSettings>()
            .ConfigurationInfo.WritePath.ShouldBe(Path.GetFullPath("existing.json"));
    }

    private sealed class RejectingFileBackend : IFileBackend
    {
        private readonly InMemoryFileBackend _inner = new();

        public bool IsPhysical => _inner.IsPhysical;

        public string GetPhysicalPath(string path) => _inner.GetPhysicalPath(path);

        public bool FileExists(string path) => _inner.FileExists(path);

        public Stream? OpenReadStream(string path) => _inner.OpenReadStream(path);

        public Task SaveToFileAsync(
            string path,
            ReadOnlyMemory<byte> content,
            ILogger? logger = null,
            CancellationToken cancellationToken = default
        ) => _inner.SaveToFileAsync(path, content, logger, cancellationToken);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public bool CanWriteToFile(string path) => _inner.CanWriteToFile(path);

        public bool CanWriteToDirectory(string path) => _inner.CanWriteToDirectory(path);

        public bool EnsureDirectoryExists(string path) => false;

        public bool TryBackup(string path, out string? backupPath, ILogger? logger = null) =>
            _inner.TryBackup(path, out backupPath, logger);

        public bool TryDelete(string path, ILogger? logger = null) =>
            _inner.TryDelete(path, logger);

        public bool TryRestoreLatestBackup(string path, ILogger? logger = null) =>
            _inner.TryRestoreLatestBackup(path, logger);
    }

    private sealed class LegacyJsonFileOptions(string extension) : JsonFileOptions
    {
        public override string FileExtension => extension;
    }
}
