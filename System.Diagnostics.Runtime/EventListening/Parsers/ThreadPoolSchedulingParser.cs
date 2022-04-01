using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ThreadPoolSchedulingParser : IEventParser<ThreadPoolSchedulingParser>, ThreadPoolSchedulingParser.Events.Verbose
{
    private const int EventIdThreadPoolWork = 30, EventIdThreadPoolDequeueWork = 31;

    private readonly EventPairTimer<long> _eventPairTimer;

    public event Action? Enqueue;
    public event Action<Events.DequeueEvent>? Dequeue;
    public event Action? Completed;

    public ThreadPoolSchedulingParser()
    {
        _eventPairTimer = new EventPairTimer<long>(
            EventIdThreadPoolWork,
            EventIdThreadPoolDequeueWork,
            x => (long)x.Payload![0]!);
    }

    public string EventSourceName => FrameworkEventSource.Name;
    public EventKeywords Keywords => (EventKeywords)(FrameworkEventSource.Keywords.ThreadPool);
#if NETFRAMEWORK
#endif
    public void ProcessEvent(EventWrittenEventArgs e)
    {
        switch (_eventPairTimer.TryGetDuration(e, out var duration))
        {
            case DurationResult.Start:
                Enqueue?.Invoke();
                return;

            case DurationResult.FinalWithDuration when duration > TimeSpan.Zero:
                Dequeue?.Invoke(new Events.DequeueEvent(duration));
                return;
        }

        if (e.EventId == EventIdThreadPoolDequeueWork) Completed?.Invoke();
    }

    public static class Events
    {
        public interface Verbose : IVerboseEvents
        {
            event Action Enqueue;
            event Action<DequeueEvent> Dequeue;
            event Action Completed;
        }

        public record struct DequeueEvent(TimeSpan EnqueueDuration);
    }
}
