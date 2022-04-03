using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ThreadPoolEventParser : IEventParser<ThreadPoolEventParser>, ThreadPoolEventParser.Events.Info
{
    private const int
        EventIdThreadPoolSample = 54,
        EventIdThreadPoolAdjustment = 55,
        EventIdIoThreadCreate = 44,
        EventIdIoThreadRetire = 46,
        EventIdIoThreadUnretire = 47,
        EventIdIoThreadTerminate = 45;

    public event Action<Events.ThreadPoolAdjustedEvent>? ThreadPoolAdjusted;
    public event Action<Events.IoThreadPoolAdjustedEvent>? IoThreadPoolAdjusted;

    public string EventSourceName => NativeRuntimeEventSource.Name;
    public EventKeywords Keywords => (EventKeywords) NativeRuntimeEventSource.Keywords.Threading;

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        switch (e.EventId)
        {
            case EventIdThreadPoolAdjustment:
                ThreadPoolAdjusted?.Invoke(new Events.ThreadPoolAdjustedEvent((uint) e.Payload![1]!, (NativeRuntimeEventSource.ThreadAdjustmentReason) e.Payload![2]!));
                return;

            case EventIdIoThreadCreate:
            case EventIdIoThreadRetire:
            case EventIdIoThreadUnretire:
            case EventIdIoThreadTerminate:
                IoThreadPoolAdjusted?.Invoke(new Events.IoThreadPoolAdjustedEvent((uint) e.Payload![0]!));
                return;
        }
    }

    public static class Events
    {
        public interface Info : IInfoEvents
        {
            event Action<ThreadPoolAdjustedEvent> ThreadPoolAdjusted;
            event Action<IoThreadPoolAdjustedEvent> IoThreadPoolAdjusted;
        }

        public record struct ThreadPoolAdjustedEvent(uint NumThreads, NativeRuntimeEventSource.ThreadAdjustmentReason AdjustmentReason);

        public record struct IoThreadPoolAdjustedEvent(uint NumThreads);
    }
}
