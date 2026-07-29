using System;
using System.Threading.Tasks;

namespace Configuration.Writable.Tests.Utility;

/// <summary>
/// Helpers for writing reliable tests that depend on <see cref="System.IO.FileSystemWatcher"/> events.
///
/// <see cref="System.IO.FileSystemWatcher"/> delivery is inherently best-effort and can take
/// noticeably longer on busy CI machines than on a developer workstation. Tests that simply
/// <c>await Task.Delay(...)</c> for a fixed interval are therefore flaky. The helpers in this
/// class instead poll for the expected side effect (or a <see cref="TaskCompletionSource{T}"/>
/// being signalled) with a generous timeout, so tests stay deterministic across platforms.
/// </summary>
public static class FileWatcherTestHelper
{
    /// <summary>
    /// Default timeout used by the helper overloads that do not take an explicit timeout.
    /// 5 seconds is enough to ride out FileSystemWatcher latency spikes and the default
    /// <c>OnChangeDebounce</c> (300 ms) on any reasonable CI environment.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Polls <paramref name="condition"/> until it returns <c>true</c> or the timeout elapses.
    /// </summary>
    /// <param name="condition">Condition to evaluate on each poll.</param>
    /// <param name="timeout">
    /// Maximum time to wait. Defaults to <see cref="DefaultTimeout"/>; pass
    /// <see cref="TimeSpan.Zero"/> to disable the wait entirely.
    /// </param>
    /// <param name="pollInterval">How often to re-check the condition.</param>
    /// <returns>
    /// The final result of <paramref name="condition"/>. Callers that need to assert
    /// success should compare the result to <c>true</c>.
    /// </returns>
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout = default,
        TimeSpan pollInterval = default
    )
    {
        if (timeout == default)
            timeout = DefaultTimeout;
        if (pollInterval == default)
            pollInterval = TimeSpan.FromMilliseconds(50);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(pollInterval).ConfigureAwait(false);
        }
        return condition();
    }

    /// <summary>
    /// Polls <paramref name="getValue"/> until it returns a non-null reference or the
    /// timeout elapses, and returns the value supplied by the listener.
    /// </summary>
    /// <typeparam name="T">Reference type produced by the listener.</typeparam>
    /// <param name="getValue">Accessor that returns the latest value supplied by the listener.</param>
    /// <param name="timeout">
    /// Maximum time to wait. Defaults to <see cref="DefaultTimeout"/>; pass
    /// <see cref="TimeSpan.Zero"/> to disable the wait entirely.
    /// </param>
    /// <param name="pollInterval">How often to re-check the value.</param>
    /// <returns>
    /// The first non-null value observed, or the final (possibly null) value of
    /// <paramref name="getValue"/> if the timeout elapses first.
    /// </returns>
    public static async Task<T?> WaitForNonNullAsync<T>(
        Func<T?> getValue,
        TimeSpan timeout = default,
        TimeSpan pollInterval = default
    )
        where T : class
    {
        if (timeout == default)
            timeout = DefaultTimeout;
        if (pollInterval == default)
            pollInterval = TimeSpan.FromMilliseconds(50);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = getValue();
            if (value is not null)
                return value;
            await Task.Delay(pollInterval).ConfigureAwait(false);
        }
        return getValue();
    }
}
