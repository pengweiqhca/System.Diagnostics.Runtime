using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ContentionEventParser : IEventParser<ContentionEventParser>, ContentionEventParser.Events.Info
{
    private readonly EventPairTimer<long> _eventPairTimer;

    public event Action<Events.ContentionEndEvent>? ContentionEnd;

    public ContentionEventParser()
    {
        _eventPairTimer = new EventPairTimer<long>(
            NativeRuntimeEventSource.EventId.ContentionStart,
            NativeRuntimeEventSource.EventId.ContentionStop,
#if NETCOREAPP
            x => x.OSThreadId
#else
            _ => Environment.CurrentManagedThreadId
#endif
        );
    }

    public EventKeywords Keywords => (EventKeywords)NativeRuntimeEventSource.Keywords.Contention;
    public string EventSourceName => NativeRuntimeEventSource.Name;

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (_eventPairTimer.TryGetDuration(e, out var duration) == DurationResult.FinalWithDuration && duration > TimeSpan.Zero)
            ContentionEnd?.Invoke(new (duration, (NativeRuntimeEventSource.ContentionFlags)(byte)e.Payload![0]!));
    }

    public static class Events
    {
        public interface Info : IInfoEvents
        {
            event Action<ContentionEndEvent> ContentionEnd;
        }

        public record struct ContentionEndEvent(TimeSpan ContentionDuration, NativeRuntimeEventSource.ContentionFlags ContentionFlags);
    }
}
