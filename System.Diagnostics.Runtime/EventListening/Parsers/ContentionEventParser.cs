using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ContentionEventParser : IEventParser<ContentionEventParser>, ContentionEventParser.Events.Info
{
    private const int EventIdContentionStart = 81, EventIdContentionStop = 91;
    private readonly EventPairTimer<long> _eventPairTimer;

    public event Action<Events.ContentionEndEvent>? ContentionEnd;

    public ContentionEventParser()
    {
        _eventPairTimer = new EventPairTimer<long>(
            EventIdContentionStart,
            EventIdContentionStop,
#if NETCOREAPP
            x => x.OSThreadId
#else
            _ => Environment.CurrentManagedThreadId
#endif
        );
    }

    public EventKeywords Keywords => (EventKeywords)NativeRuntimeEventSource.Keywords.Contention;
    public string EventSourceName => NativeRuntimeEventSource.Name;
#if NETFRAMEWORK
#endif
    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (_eventPairTimer.TryGetDuration(e, out var duration) == DurationResult.FinalWithDuration && duration > TimeSpan.Zero)
            ContentionEnd?.Invoke(new Events.ContentionEndEvent(duration));
    }

    public static class Events
    {
        public interface Info : IInfoEvents
        {
            event Action<ContentionEndEvent> ContentionEnd;
        }

        public record struct ContentionEndEvent(TimeSpan ContentionDuration);
    }
}
