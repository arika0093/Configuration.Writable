using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.Configure;
using Configuration.Writable.Options;
using Configuration.Writable.State;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.Tests;

public partial class WritableOptionsSaveCoordinationTests
{
    [Test]
    public async Task SaveAsync_ClonesInputBeforePublishingCache()
    {
        var fileProvider = new InMemoryFileBackend();
        var instance = new WritableOptionsSimpleInstance<FirstSettings>();
        instance.Initialize(options =>
        {
            options.FilePath = $"clone-{Guid.NewGuid():N}.json";
            options.UseInMemoryBackend(fileProvider);
            options.UseJsonCloneStrategy();
        });
        var options = instance.GetOptions();
        var input = new FirstSettings { Value = "saved" };

        await options.SaveAsync(input);
        input.Value = "mutated by caller";

        options.CurrentValue.Value.ShouldBe("saved");
    }

    [Test]
    public async Task SaveAsync_SerializesSavesForTheSameNormalizedPathAcrossOptionTypes()
    {
        var provider = new BlockingFileBackend(expectedSaveCount: 2, new InMemoryFileBackend());
        var path = $"shared-save-{Guid.NewGuid():N}.json";
        var first = CreateOptions<FirstSettings>(provider, path);
        var second = CreateOptions<SecondSettings>(provider, path);

        var firstSave = first.SaveAsync(new FirstSettings { Value = "first" });
        await AwaitWithTimeout(provider.FirstSaveEntered.Task, TimeSpan.FromSeconds(2));

        var secondSave = second.SaveAsync(new SecondSettings { Value = "second" });
        await Task.Delay(100);
        provider.SaveCount.ShouldBe(1);

        provider.Release();
        await Task.WhenAll(firstSave, secondSave);
        provider.SaveCount.ShouldBe(2);

        Dispose(first);
        Dispose(second);
    }

    [Test]
    public async Task SaveAsync_AllowsDifferentFilesToSaveInParallel()
    {
        var provider = new BlockingFileBackend(expectedSaveCount: 2, new InMemoryFileBackend());
        var first = CreateOptions<FirstSettings>(provider, $"first-{Guid.NewGuid():N}.json");
        var second = CreateOptions<SecondSettings>(provider, $"second-{Guid.NewGuid():N}.json");

        var firstSave = first.SaveAsync(new FirstSettings { Value = "first" });
        var secondSave = second.SaveAsync(new SecondSettings { Value = "second" });

        await AwaitWithTimeout(provider.AllSavesEntered.Task, TimeSpan.FromSeconds(2));
        provider.SaveCount.ShouldBe(2);
        provider.Release();
        await Task.WhenAll(firstSave, secondSave);

        Dispose(first);
        Dispose(second);
    }

    [Test]
    public async Task SaveAsync_DeletesSidecarLockAfterSaving()
    {
        var provider = new BlockingFileBackend(expectedSaveCount: 1, new InMemoryFileBackend());
        var path = Path.Combine(AppContext.BaseDirectory, $"sidecar-lock-{Guid.NewGuid():N}.json");
        var options = CreateOptions<FirstSettings>(provider, path);

        try
        {
            var save = options.SaveAsync(new FirstSettings { Value = "saved" });
            await AwaitWithTimeout(provider.FirstSaveEntered.Task, TimeSpan.FromSeconds(2));

            File.Exists(path + ".lock").ShouldBeTrue();

            provider.Release();
            await save;
            File.Exists(path + ".lock").ShouldBeFalse();
        }
        finally
        {
            Dispose(options);
        }
    }

    [Test]
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

    [Test]
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
        IFileBackend backend,
        string path
    )
        where T : class, new()
    {
        var instance = new WritableOptionsSimpleInstance<T>();
        instance.Initialize(options =>
        {
            options.FilePath = path;
            options.FileBackend = backend;
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
        var registry = new WritableOptionsRegistry<FirstSettings>([configuration]);
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

    [OptionsModel]
    internal partial class FirstSettings
    {
        public string Value { get; set; } = "";
    }

    [OptionsModel]
    internal partial class SecondSettings
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

    private sealed class BlockingFileBackend(
        int expectedSaveCount,
        InMemoryFileBackend inner
    ) : IFileBackend
    {
        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private int _saveCount;

        internal TaskCompletionSource<bool> FirstSaveEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> AllSavesEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int SaveCount => Volatile.Read(ref _saveCount);

        public bool IsPhysical => inner.IsPhysical;

        public string GetPhysicalPath(string path) => inner.GetPhysicalPath(path);

        public bool FileExists(string path) => inner.FileExists(path);

        public Stream? OpenReadStream(string path) => inner.OpenReadStream(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public bool CanWriteToFile(string path) => inner.CanWriteToFile(path);

        public bool CanWriteToDirectory(string path) => inner.CanWriteToDirectory(path);

        public bool EnsureDirectoryExists(string path) => inner.EnsureDirectoryExists(path);

        public bool TryBackup(string path, out string? backupPath, ILogger? logger = null) =>
            inner.TryBackup(path, out backupPath, logger);

        public bool TryDelete(string path, ILogger? logger = null) =>
            inner.TryDelete(path, logger);

        public bool TryRestoreLatestBackup(string path, ILogger? logger = null) =>
            inner.TryRestoreLatestBackup(path, logger);

        public async Task SaveToFileAsync(
            string path,
            ReadOnlyMemory<byte> content,
            ILogger? logger = null,
            CancellationToken cancellationToken = default
        )
        {
            var saveCount = Interlocked.Increment(ref _saveCount);
            FirstSaveEntered.TrySetResult(true);
            if (saveCount == expectedSaveCount)
            {
                AllSavesEntered.TrySetResult(true);
            }
            await AwaitWithCancellation(_release.Task, cancellationToken);
        }

        internal void Release() => _release.TrySetResult(true);
    }

    private static async Task AwaitWithTimeout(Task task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
        {
            throw new TimeoutException($"Task did not complete within {timeout}.");
        }
        await task;
    }

    private static async Task AwaitWithCancellation(Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await task;
            return;
        }
        var tcs = new TaskCompletionSource<bool>();
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());
        var completed = await Task.WhenAny(task, tcs.Task);
        if (completed != task)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        await task;
    }
}
