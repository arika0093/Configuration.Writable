using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.State;

namespace Configuration.Writable.Tests.State;

public class CompositeStateSourceTests
{
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
                StateFallbackCondition.NotFound
            ),
            new StateSource<string>(
                "primary",
                primary,
                primary,
                null,
                100,
                StateFallbackCondition.NotFound
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
                StateFallbackCondition.NotFound
            ),
            new StateSource<string>(
                "fallback",
                fallback,
                fallback,
                null,
                0,
                StateFallbackCondition.NotFound
            ),
        ]
        );

        var read = await source.ReadAsync();
        await source.WriteAsync(new StateWriteRequest<string>("saved", read.Revision));

        primary.LastWriteRequest.ShouldBe(new StateWriteRequest<string>("saved", "primary-r1"));
        fallback.LastWriteRequest.ShouldBeNull();
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
                StateFallbackCondition.NotFound
            ),
            new StateSource<string>(
                "fallback",
                fallback,
                fallback,
                null,
                0,
                StateFallbackCondition.NotFound
            ),
        ]
        );

        var result = await source.ReadAsync();

        result.Status.ShouldBe(StateReadStatus.Unavailable);
        fallback.ReadCount.ShouldBe(0);
    }

    private sealed class TestSource<T>(StateReadResult<T> readResult) : IStateReader<T>, IStateWriter<T>
    {
        internal int ReadCount { get; private set; }

        internal StateWriteRequest<T>? LastWriteRequest { get; private set; }

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
    }
}
