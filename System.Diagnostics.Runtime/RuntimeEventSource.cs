using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "RuntimeEventSource")]
internal class RuntimeEventSource : EventSource
{
    public static readonly RuntimeEventSource Log = new();

    [NonEvent]
    public void EtlConstructException(Exception ex)
    {
        if (IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            EtlConstructException(ex.Message);
    }

    [Event(1, Message = "Construct EtlParser threw exception. Exception {0}.", Level = EventLevel.Error)]
    private void EtlConstructException(string exception) => WriteEvent(1, exception);
}
