using System.Diagnostics.Tracing;

namespace Configuration.Writable.Diagnostics;

[EventSource(Name = "Configuration-Writable")]
internal sealed class ConfigurationWritableEventSource : EventSource
{
    internal static readonly ConfigurationWritableEventSource Log = new();

    private readonly EventCounter _saveDurationMilliseconds;

    private ConfigurationWritableEventSource()
    {
        _saveDurationMilliseconds = new EventCounter("save-duration-ms", this);
    }

    internal void SaveSucceeded(double durationMilliseconds)
    {
        _saveDurationMilliseconds.WriteMetric((float)durationMilliseconds);
        SaveSucceededEvent(durationMilliseconds);
    }

    internal void SaveFailed() => SaveFailedEvent();

    internal void ReloadFailed() => ReloadFailedEvent();

    internal void ConflictDetected() => ConflictDetectedEvent();

    [Event(1, Level = EventLevel.Informational)]
    private void SaveSucceededEvent(double durationMilliseconds) =>
        WriteEvent(1, durationMilliseconds);

    [Event(2, Level = EventLevel.Warning)]
    private void SaveFailedEvent() => WriteEvent(2);

    [Event(3, Level = EventLevel.Warning)]
    private void ReloadFailedEvent() => WriteEvent(3);

    [Event(4, Level = EventLevel.Warning)]
    private void ConflictDetectedEvent() => WriteEvent(4);
}
