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
    public async Task ReadAsync_StopsWhenFallbackConditionDoesNotMatch()
    {
        var primary = new TestSource<string>(StateReadResult<string>.Unavailable());
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

        result.Status.ShouldBe(StateReadStatus.Unavailable);
        primary.ReadCount.ShouldBe(1);
        fallback.ReadCount.ShouldBe(0);
    }

    [Test]
    public async Task WriteAsync_UsesConfiguredWriteTargetAndRevision()
    {
        var primary = new TestSource<string>(StateReadResult<string>.Success("primary", "primary-r1"));
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
        ],
            "fallback"
        );

        var loaded = await source.ReadAsync();
        await source.WriteAsync(new StateWriteRequest<string>("written", loaded.Revision));

        primary.WriteCount.ShouldBe(0);
        fallback.WriteCount.ShouldBe(1);
        fallback.LastExpectedRevision.ShouldBeNull();
    }

    [Test]
    public void FromProvider_GeneratesAnUnusedProviderId()
    {
        var builder = new WritableOptionsConfigBuilder<TestSettings>();
        builder.FromProvider(
            new ReadOnlyTestSource<TestSettings>(),
            "provider-1",
            priority: 100,
            fallbackCondition: StateFallbackConditions.NotFound
        );
        builder.FromProvider(
            new ReadOnlyTestSource<TestSettings>(),
            priority: 50,
            fallbackCondition: StateFallbackConditions.NotFound
        );
        builder.UseWriteTarget("provider-2");

        Should.NotThrow(() => builder.BuildOptions("").CreateStateSource());
    }

    [Test]
    public void UseWriteTarget_RejectsUnknownTargetWithoutConfiguredProviders()
    {
        var builder = new WritableOptionsConfigBuilder<TestSettings>();
        builder.UseWriteTarget("remote");

        Should.Throw<ArgumentException>(() => builder.BuildOptions("").CreateStateSource());
    }

    [Test]
    public void OptionsMonitor_UsesDefaultValueForNotFoundState()
    {
        var services = new ServiceCollection();
        services
            .AddWritableOptions<TestSettings>(builder =>
            {
                builder.FromProvider(
                    new ReadOnlyTestSource<TestSettings>(),
                    "remote",
                    priority: 100,
                    fallbackCondition: StateFallbackConditions.None
                );
            })
            .ValidateDataAnnotations();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWritableOptions<TestSettings>>().CurrentValue.ShouldNotBeNull();
    }

    [Test]
    public async Task WaitForChangeAsync_CancelsPendingWatcherWhenLaterWatcherThrowsSynchronously()
    {
        var pendingWatcher = new PendingWatcher();
        var source = new CompositeStateSource<string>(
        [
            new StateSource<string>(
                "pending",
                new TestSource<string>(StateReadResult<string>.NotFound("pending-r1")),
                null,
                pendingWatcher,
                100,
                StateFallbackConditions.NotFound
            ),
            new StateSource<string>(
                "throwing",
                new TestSource<string>(StateReadResult<string>.NotFound("throwing-r1")),
                null,
                new ThrowingWatcher(),
                100,
                StateFallbackConditions.NotFound
            ),
        ]
        );

        await Should.ThrowAsync<InvalidOperationException>(() =>
            source.WaitForChangeAsync(null).AsTask()
        );

        pendingWatcher.CancellationObserved.Task.IsCompleted.ShouldBeTrue();
    }

    private sealed class TestSource<T> : IStateReader<T>, IStateWriter<T>
    {
        private readonly StateReadResult<T> _readResult;

        public TestSource(StateReadResult<T> readResult)
        {
            _readResult = readResult;
        }

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public string? LastExpectedRevision { get; private set; }

        public ValueTask<StateReadResult<T>> ReadAsync(
            CancellationToken cancellationToken = default
        )
        {
            ReadCount++;
            return new ValueTask<StateReadResult<T>>(_readResult);
        }

        public ValueTask<StateWriteResult> WriteAsync(
            StateWriteRequest<T> request,
            CancellationToken cancellationToken = default
        )
        {
            WriteCount++;
            LastExpectedRevision = request.ExpectedRevision;
            return new ValueTask<StateWriteResult>(new StateWriteResult("written-r1"));
        }
    }

    private sealed class ReadOnlyTestSource<T> : IStateReader<T>, IStateWatcher
        where T : class, new()
    {
        public ValueTask<StateReadResult<T>> ReadAsync(
            CancellationToken cancellationToken = default
        ) => new(StateReadResult<T>.NotFound());

        public async ValueTask WaitForChangeAsync(
            string? observedRevision,
            CancellationToken cancellationToken = default
        ) => await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
    }

    private sealed class PendingWatcher : IStateWatcher
    {
        public TaskCompletionSource<bool> CancellationObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public async ValueTask WaitForChangeAsync(
            string? observedRevision,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }
        }
    }

    private sealed class ThrowingWatcher : IStateWatcher
    {
        public ValueTask WaitForChangeAsync(
            string? observedRevision,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Synchronous watcher failure.");
    }
}
