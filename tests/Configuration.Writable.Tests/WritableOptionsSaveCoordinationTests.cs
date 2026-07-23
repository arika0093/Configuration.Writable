using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Options;

namespace Configuration.Writable.Tests;

public class WritableOptionsSaveCoordinationTests
{
    [Fact]
    public async Task SaveAsync_ClonesInputBeforePublishingCache()
    {
        var fileProvider = new InMemoryFileProvider();
        var instance = new WritableOptionsSimpleInstance<FirstSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = $"clone-{Guid.NewGuid():N}.json";
            options.FileProvider = fileProvider;
            options.UseJsonCloneStrategy();
        });
        var options = instance.GetOptions();
        var input = new FirstSettings { Value = "saved" };

        await options.SaveAsync(input);
        input.Value = "mutated by caller";

        options.CurrentValue.Value.ShouldBe("saved");
    }

    [Fact]
    public async Task SaveAsync_SerializesSavesForTheSameNormalizedPathAcrossOptionTypes()
    {
        var provider = new BlockingFormatProvider(expectedSaveCount: 2);
        var path = $"shared-save-{Guid.NewGuid():N}.json";
        var first = CreateOptions<FirstSettings>(provider, path);
        var second = CreateOptions<SecondSettings>(provider, path);

        var firstSave = first.SaveAsync(new FirstSettings { Value = "first" });
        await provider.FirstSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondSave = second.SaveAsync(new SecondSettings { Value = "second" });
        await Task.Delay(100);
        provider.SaveCount.ShouldBe(1);

        provider.Release();
        await Task.WhenAll(firstSave, secondSave);
        provider.SaveCount.ShouldBe(2);

        Dispose(first);
        Dispose(second);
    }

    [Fact]
    public async Task SaveAsync_AllowsDifferentFilesToSaveInParallel()
    {
        var provider = new BlockingFormatProvider(expectedSaveCount: 2);
        var first = CreateOptions<FirstSettings>(provider, $"first-{Guid.NewGuid():N}.json");
        var second = CreateOptions<SecondSettings>(provider, $"second-{Guid.NewGuid():N}.json");

        var firstSave = first.SaveAsync(new FirstSettings { Value = "first" });
        var secondSave = second.SaveAsync(new SecondSettings { Value = "second" });

        await provider.AllSavesEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        provider.SaveCount.ShouldBe(2);
        provider.Release();
        await Task.WhenAll(firstSave, secondSave);

        Dispose(first);
        Dispose(second);
    }

    [Fact]
    public async Task SaveAsync_HoldsSidecarLockWhileSaving()
    {
        var provider = new BlockingFormatProvider(expectedSaveCount: 1);
        var path = Path.Combine(AppContext.BaseDirectory, $"sidecar-lock-{Guid.NewGuid():N}.json");
        var options = CreateOptions<FirstSettings>(provider, path);

        try
        {
            var save = options.SaveAsync(new FirstSettings { Value = "saved" });
            await provider.FirstSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            File.Exists(path + ".lock").ShouldBeTrue();

            provider.Release();
            await save;
        }
        finally
        {
            Dispose(options);
            File.Delete(path + ".lock");
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsExternalChangesByDefault()
    {
        var path = GetFilePath();
        try
        {
            File.WriteAllText(path, """{"Value":"loaded"}""");
            var fileOptions = CreateFileOptions(
                path,
                ConfigurationConflictResolution.FailOnConflict
            );
            var monitorLock = GetMonitorLock(fileOptions.Monitor);
            monitorLock.Wait();
            try
            {
                File.WriteAllText(path, """{"Value":"external"}""");

                await Should.ThrowAsync<ConfigurationConflictException>(() =>
                    fileOptions.Options.SaveAsync(new FirstSettings { Value = "saved" })
                );
            }
            finally
            {
                monitorLock.Release();
                fileOptions.Monitor.Dispose();
            }
        }
        finally
        {
            DeleteFiles(path);
        }
    }

    [Fact]
    public async Task SaveAsync_CanUseLastWriteWinsForExternalChanges()
    {
        var path = GetFilePath();
        try
        {
            File.WriteAllText(path, """{"Value":"loaded"}""");
            var fileOptions = CreateFileOptions(
                path,
                ConfigurationConflictResolution.LastWriteWins
            );

            File.WriteAllText(path, """{"Value":"external"}""");

            await fileOptions.Options.SaveAsync(new FirstSettings { Value = "saved" });
            fileOptions.Options.CurrentValue.Value.ShouldBe("saved");
            fileOptions.Monitor.Dispose();
        }
        finally
        {
            DeleteFiles(path);
        }
    }

    private static IWritableOptionsMonitor<T> CreateOptions<T>(
        IWritableFormatProvider formatProvider,
        string path
    )
        where T : class, new()
    {
        var instance = new WritableOptionsSimpleInstance<T>();
        instance.Initialize(options =>
        {
            options.FilePath = path;
            options.FormatProvider = formatProvider;
            options.UseJsonCloneStrategy();
        });
        return instance.GetOptions();
    }

    private static FileOptions CreateFileOptions(
        string path,
        ConfigurationConflictResolution conflictResolution
    )
    {
        var builder = new WritableOptionsConfigBuilder<FirstSettings>
        {
            FilePath = path,
            ConflictResolution = conflictResolution,
        };
        builder.UseJsonCloneStrategy();
        var configuration = builder.BuildOptions(Microsoft.Extensions.Options.Options.DefaultName);
        var registry = new WritableOptionsConfigRegistryImpl<FirstSettings>([configuration]);
        var monitor = new OptionsMonitorImpl<FirstSettings>(registry);
        return new FileOptions(new WritableOptionsImpl<FirstSettings>(monitor, registry), monitor);
    }

    private static SemaphoreSlim GetMonitorLock(OptionsMonitorImpl<FirstSettings> monitor) =>
        (SemaphoreSlim)
            typeof(OptionsMonitorImpl<FirstSettings>)
                .GetField("_semaphore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(monitor)!;

    private static string GetFilePath() =>
        Path.Combine(AppContext.BaseDirectory, $"save-coordination-{Guid.NewGuid():N}.json");

    private static void DeleteFiles(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var prefix = Path.GetFileNameWithoutExtension(path);
        foreach (var file in Directory.GetFiles(directory, $"{prefix}*"))
        {
            File.Delete(file);
        }
    }

    private static void Dispose(object options) => (options as IDisposable)?.Dispose();

    private sealed class FirstSettings
    {
        public string Value { get; set; } = "";
    }

    private sealed class SecondSettings
    {
        public string Value { get; set; } = "";
    }

    private sealed class FileOptions(
        IWritableOptionsMonitor<FirstSettings> options,
        OptionsMonitorImpl<FirstSettings> monitor
    )
    {
        internal IWritableOptionsMonitor<FirstSettings> Options { get; } = options;
        internal OptionsMonitorImpl<FirstSettings> Monitor { get; } = monitor;
    }

    private sealed class BlockingFormatProvider(int expectedSaveCount) : JsonFormatProvider
    {
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private int _saveCount;

        internal TaskCompletionSource FirstSaveEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource AllSavesEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int SaveCount => Volatile.Read(ref _saveCount);

        public override async Task SaveAsync<T>(
            T config,
            IWritableOptionsConfiguration options,
            CancellationToken cancellationToken = default
        )
        {
            var saveCount = Interlocked.Increment(ref _saveCount);
            FirstSaveEntered.TrySetResult();
            if (saveCount == expectedSaveCount)
            {
                AllSavesEntered.TrySetResult();
            }
            await _release.Task.WaitAsync(cancellationToken);
        }

        internal void Release() => _release.TrySetResult();
    }
}
