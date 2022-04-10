using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ThreadPoolEventParser : IEventParser<ThreadPoolEventParser>, ThreadPoolEventParser.Events.Info
{ public event Action<Events.ThreadPoolAdjustedEvent>? ThreadPoolAdjusted;
    public event Action<Events.IoThreadPoolAdjustedEvent>? IoThreadPoolAdjusted;

    public string EventSourceName => NativeRuntimeEventSource.Name;
    public EventKeywords Keywords => (EventKeywords) NativeRuntimeEventSource.Keywords.Threading;

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        switch (e.EventId)
        {
            case NativeRuntimeEventSource.EventId.ThreadPoolAdjustment:
                ThreadPoolAdjusted?.Invoke(new ((uint) e.Payload![1]!, (NativeRuntimeEventSource.ThreadAdjustmentReason) e.Payload![2]!));
                return;

            case NativeRuntimeEventSource.EventId.IoThreadCreate:
            case NativeRuntimeEventSource.EventId.IoThreadRetire:
            case NativeRuntimeEventSource.EventId.IoThreadUnretire:
            case NativeRuntimeEventSource.EventId.IoThreadTerminate:
                IoThreadPoolAdjusted?.Invoke(new ((uint) e.Payload![0]!));
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
