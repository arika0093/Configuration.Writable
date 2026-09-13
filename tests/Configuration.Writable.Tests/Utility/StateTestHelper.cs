using System.Threading.Tasks;
using Configuration.Writable.State;
using Shouldly;

namespace Configuration.Writable.Tests.Utility;

/// <summary>
/// Reads and writes state snapshots directly through a registration's state
/// source. Used by tests that previously drove format providers directly.
/// </summary>
public static class StateTestHelper
{
    public static T ReadStateValue<T>(WritableOptionsConfiguration<T> options)
        where T : class, new()
    {
        var result = options.CreateStateSource().ReadAsync().AsTask().GetAwaiter().GetResult();
        result.Status.ShouldBe(StateReadStatus.Success);
        return result.Value!;
    }

    public static Task WriteStateValue<T>(WritableOptionsConfiguration<T> options, T value)
        where T : class, new() =>
        options.CreateStateSource().WriteAsync(new StateWriteRequest<T>(value, null)).AsTask();
}
