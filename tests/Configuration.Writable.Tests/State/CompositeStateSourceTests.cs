using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Internal;
using Configuration.Writable.State;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.Writable.Tests.State;

public class CompositeStateSourceTests
{
    [Test]
    public async Task FileStateSource_UsesRevisionForOptimisticConcurrency()
    {
        using var file = new TemporaryFile();
        var options = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = file.FilePath,
            FileProvider = new CommonFileProvider(),
            FormatProvider = new JsonFormatProvider(),
        }.BuildOptions("");
        var source = new FileStateSource<TestSettings>(options);

        await source.WriteAsync(new StateWriteRequest<TestSettings>(new TestSettings { Value = "one" }, null));
        var loaded = await source.ReadAsync();
        File.WriteAllText(options.ConfigFilePath, "{\"Value\":\"external\"}");

        await Should.ThrowAsync<ConfigurationConflictException>(() =>
            source
                .WriteAsync(new StateWriteRequest<TestSettings>(new TestSettings { Value = "two" }, loaded.Revision))
                .AsTask()
        );
    }

    [Test]
    public async Task FileStateSource_ReturnsNotFoundWhenFileIsMissing()
    {
        using var file = new TemporaryFile();
        File.Delete(file.FilePath);
        var options = new WritableOptionsConfigBuilder<TestSettings>
        {
            FilePath = file.FilePath,
            FileProvider = new CommonFileProvider(),
            FormatProvider = new JsonFormatProvider(),
        }.BuildOptions("");
        var source = new FileStateSource<TestSettings>(options);

        var result = await source.ReadAsync();

        result.Status.ShouldBe(StateReadStatus.NotFound);
        result.Value.ShouldBeNull();
    }

    [Test]
    public async Task ReadAsync_UsesPriorityAndConfiguredFallback()
    {
        var primary = new TestSource<string>(StateReadResult<string>.NotFound("primary-r1"));
        var fallback = new TestSource<string>(StateReadResult<string>.Success("fallback", "fallback-r1"));
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>(
                "fallback",
                fallback,
                fallback,
                null,
                0,
                StateFallbackConditions.NotFound
            ),
            new StateSource<string>(
                "primary",
                primary,
                primary,
                null,
                100,
                StateFallbackConditions.NotFound
            ),
        ]
        );

        var result = await source.ReadAsync();

        result.Status.ShouldBe(StateReadStatus.Success);
        result.Value.ShouldBe("fallback");
        primary.ReadCount.ShouldBe(1);
        fallback.ReadCount.ShouldBe(1);
    }

    [Test]
    public async Task WriteAsync_UsesWriteTargetRevisionCollectedDuringFallbackRead()
    {
        var primary = new TestSource<string>(StateReadResult<string>.NotFound("primary-r1"));
        var fallback = new TestSource<string>(StateReadResult<string>.Success("fallback", "fallback-r1"));
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>(
                "primary",
                primary,
                primary,
                null,
                100,
                StateFallbackConditions.NotFound
            ),
            new StateSource<string>(
                "fallback",
                fallback,
                fallback,
                null,
                0,
                StateFallbackConditions.NotFound
            ),
        ]
        );

        var read = await source.ReadAsync();
        await source.WriteAsync(new StateWriteRequest<string>("saved", read.Revision));

        primary.LastWriteRequest.ShouldBe(new StateWriteRequest<string>("saved", "primary-r1"));
        fallback.LastWriteRequest.ShouldBeNull();
    }

    [Test]
    public async Task WriteAsync_UsesExplicitWriteTarget()
    {
        var primary = new TestSource<string>(StateReadResult<string>.Success("primary", "primary-r1"));
        var fallback = new TestSource<string>(StateReadResult<string>.Success("fallback", "fallback-r1"));
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>("primary", primary, primary, null, 100, StateFallbackConditions.NotFound),
            new StateSource<string>("fallback", fallback, fallback, null, 0, StateFallbackConditions.NotFound),
        ],
            writeTargetId: "fallback"
        );

        await source.WriteAsync(new StateWriteRequest<string>("saved", null));

        primary.LastWriteRequest.ShouldBeNull();
        fallback.LastWriteRequest.ShouldNotBeNull();
        var write = fallback.LastWriteRequest!.Value;
        write.Value.ShouldBe("saved");
    }

    [Test]
    public async Task ReadAsync_DoesNotFallbackWhenTheConfiguredConditionDoesNotMatch()
    {
        var primary = new TestSource<string>(StateReadResult<string>.Unavailable());
        var fallback = new TestSource<string>(StateReadResult<string>.Success("fallback"));
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>(
                "primary",
                primary,
                primary,
                null,
                100,
                StateFallbackConditions.NotFound
            ),
            new StateSource<string>(
                "fallback",
                fallback,
                fallback,
                null,
                0,
                StateFallbackConditions.NotFound
            ),
        ]
        );

        var result = await source.ReadAsync();

        result.Status.ShouldBe(StateReadStatus.Unavailable);
        fallback.ReadCount.ShouldBe(0);
    }

    [Test]
    public async Task FromProvider_UsesSourceForReadAndWriteWithItsRevision()
    {
        var source = new TestSource<TestSettings>(
            StateReadResult<TestSettings>.Success(new TestSettings { Value = "remote" }, "r1")
        );
        var services = new ServiceCollection();
        services.AddWritableOptions<TestSettings>(options => options.FromProvider("remote", source));
        using var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredService<IWritableOptions<TestSettings>>();

        writableOptions.CurrentValue.Value.ShouldBe("remote");
        await writableOptions.SaveAsync(new TestSettings { Value = "updated" });

        source.LastWriteRequest.ShouldNotBeNull();
        var request = source.LastWriteRequest!.Value;
        request.Value.Value.ShouldBe("updated");
        request.ExpectedRevision.ShouldBe("r1");
    }

    [Test]
    public void FromProvider_GeneratesAnUnusedProviderId()
    {
        var explicitSource = new TestSource<TestSettings>(
            StateReadResult<TestSettings>.Success(new TestSettings(), "explicit-r1")
        );
        var generatedSource = new TestSource<TestSettings>(
            StateReadResult<TestSettings>.Success(new TestSettings(), "generated-r1")
        );
        var builder = new WritableOptionsConfigBuilder<TestSettings>();
        builder.FromProvider("provider-1", explicitSource);
        builder.FromProvider(generatedSource);
        builder.UseWriteTarget("provider-2");
        var options = builder.BuildOptions("");

        var source = options.CreateStateSource();

        source.ShouldNotBeNull();
    }

    [Test]
    public void UseWriteTarget_RejectsUnknownTargetWithoutConfiguredProviders()
    {
        var builder = new WritableOptionsConfigBuilder<TestSettings>();
        builder.UseWriteTarget("remote");
        var options = builder.BuildOptions("");

        Should.Throw<ArgumentException>(() => options.CreateStateSource());
    }

    [Test]
    public void OptionsMonitor_UsesDefaultValueForNotFoundState()
    {
        var source = new ReadOnlyTestSource<TestSettings>(StateReadResult<TestSettings>.NotFound());
        var services = new ServiceCollection();
        services.AddWritableOptions<TestSettings>(options =>
            options.FromProvider(
                "remote",
                source,
                fallbackCondition: StateFallbackConditions.None
            )
        );
        using var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredService<IWritableOptions<TestSettings>>();

        writableOptions.CurrentValue.Value.ShouldBe("");
    }

    [Test]
    public async Task WaitForChangeAsync_WatchesActiveSourceAndHigherPrioritiesOnly()
    {
        var primary = new TestSource<string>(StateReadResult<string>.Success("primary", "primary-r1"));
        var fallback = new TestSource<string>(StateReadResult<string>.Success("fallback", "fallback-r1"));
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>(
                "primary",
                primary,
                primary,
                primary,
                100,
                StateFallbackConditions.NotFound
            ),
            new StateSource<string>(
                "fallback",
                fallback,
                fallback,
                fallback,
                0,
                StateFallbackConditions.NotFound
            ),
        ]
        );

        var read = await source.ReadAsync();
        await source.WaitForChangeAsync(read.Revision);

        primary.WatchCount.ShouldBe(1);
        fallback.WatchCount.ShouldBe(0);
    }

    [Test]
    public async Task WaitForChangeAsync_CancelsPendingWatcherWhenLaterWatcherThrowsSynchronously()
    {
        var pendingReader = new ReadOnlyTestSource<string>(StateReadResult<string>.Success("one"));
        var throwingReader = new ReadOnlyTestSource<string>(StateReadResult<string>.Success("two"));
        var pendingWatcher = new PendingWatcher();
        var throwingWatcher = new ThrowingWatcher();
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>(
                "pending",
                pendingReader,
                null,
                pendingWatcher,
                100,
                StateFallbackConditions.NotFound
            ),
            new StateSource<string>(
                "throwing",
                throwingReader,
                null,
                throwingWatcher,
                0,
                StateFallbackConditions.NotFound
            ),
        ]
        );

        await Should.ThrowAsync<InvalidOperationException>(() =>
            source.WaitForChangeAsync(null).AsTask()
        );
        await pendingWatcher.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private sealed class TestSource<T>(StateReadResult<T> readResult)
        : IStateReader<T>, IStateWriter<T>, IStateWatcher
    {
        internal int ReadCount { get; private set; }

        internal StateWriteRequest<T>? LastWriteRequest { get; private set; }

        internal int WatchCount { get; private set; }

        public ValueTask<StateReadResult<T>> ReadAsync(
            CancellationToken cancellationToken = default
        )
        {
            ReadCount++;
            return new ValueTask<StateReadResult<T>>(readResult);
        }

        public ValueTask<StateWriteResult> WriteAsync(
            StateWriteRequest<T> request,
            CancellationToken cancellationToken = default
        )
        {
            LastWriteRequest = request;
            return new ValueTask<StateWriteResult>(new StateWriteResult("written-r1"));
        }

        public ValueTask WaitForChangeAsync(
            string? observedRevision,
            CancellationToken cancellationToken = default
        )
        {
            WatchCount++;
            return default;
        }
    }

    private sealed class ReadOnlyTestSource<T>(StateReadResult<T> readResult) : IStateReader<T>
    {
        public ValueTask<StateReadResult<T>> ReadAsync(
            CancellationToken cancellationToken = default
        ) => new(readResult);
    }

    private sealed class PendingWatcher : IStateWatcher
    {
        internal TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask WaitForChangeAsync(
            string? observedRevision,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.Register(() => CancellationObserved.TrySetResult(true));
            return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        }
    }

    private sealed class ThrowingWatcher : IStateWatcher
    {
        public ValueTask WaitForChangeAsync(
            string? observedRevision,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("watcher failed synchronously");
    }

    private sealed class TestSettings
    {
        public string Value { get; set; } = "";
    }
}
