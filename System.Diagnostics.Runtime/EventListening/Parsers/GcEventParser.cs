using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class GcEventParser : IEventParser<GcEventParser>, GcEventParser.Events.Info
{
    private const int
        EventIdGcStart = 1,
        EventIdGcStop = 2,
        EventIdSuspendEEStart = 9,
        EventIdRestartEEStop = 3,
        EventIdHeapStats = 4,
        EventIdAllocTick = 10;

    private readonly EventPairTimer<uint, GcData> _gcEventTimer = new(
        EventIdGcStart,
        EventIdGcStop,
        x => (uint)x.Payload![0]!,
        x => new GcData((uint)x.Payload![1]!, (NativeRuntimeEventSource.GCType)x.Payload![3]!));

    private readonly EventPairTimer<int> _gcPauseEventTimer = new(
        EventIdSuspendEEStart,
        EventIdRestartEEStop,
        // Suspensions/ Resumptions are always done sequentially so there is no common value to match events on. Return a constant value as the event id.
        x => 1);

    public event Action<Events.HeapStatsEvent>? HeapStats;
    public event Action<Events.PauseCompleteEvent>? PauseComplete;
    public event Action<Events.CollectionStartEvent>? CollectionStart;
    public event Action<Events.CollectionCompleteEvent>? CollectionComplete;

    public string EventSourceName => NativeRuntimeEventSource.Name;
    public EventKeywords Keywords => (EventKeywords)NativeRuntimeEventSource.Keywords.GC;

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (e.EventId == EventIdAllocTick) return;

        if (e.EventId == EventIdHeapStats)
        {
            HeapStats?.Invoke(new Events.HeapStatsEvent(e));

            return;
        }

        // flags representing the "Garbage Collection" + "Preparation for garbage collection" pause reasons
        const uint suspendGcReasons = 0x1 | 0x6;

        if (e.EventId == EventIdSuspendEEStart && ((uint)e.Payload![0]! & suspendGcReasons) == 0)
            return; // Execution engine is pausing for a reason other than GC, discard event.

        if (_gcPauseEventTimer.TryGetDuration(e, out var pauseDuration) == DurationResult.FinalWithDuration && pauseDuration > TimeSpan.Zero)
        {
            PauseComplete?.Invoke(new Events.PauseCompleteEvent(pauseDuration));
            return;
        }

        if (e.EventId == EventIdGcStart)
            CollectionStart?.Invoke(new Events.CollectionStartEvent((uint)e.Payload![0]!, (uint)e.Payload![1]!, (NativeRuntimeEventSource.GCReason)e.Payload![2]!));

        if (_gcEventTimer.TryGetDuration(e, out var gcDuration, out var gcData) == DurationResult.FinalWithDuration && gcDuration > TimeSpan.Zero)
            CollectionComplete?.Invoke(new Events.CollectionCompleteEvent(gcData.Generation, gcData.Type, gcDuration));
    }

    private struct GcData
    {
        public GcData(uint generation, NativeRuntimeEventSource.GCType type)
        {
            Generation = generation;
            Type = type;
        }

        public uint Generation { get; }
        public NativeRuntimeEventSource.GCType Type { get; }
    }

    public static class Events
    {
        public interface Info : IInfoEvents
        {
            event Action<HeapStatsEvent> HeapStats;
            event Action<PauseCompleteEvent> PauseComplete;
            event Action<CollectionStartEvent> CollectionStart;
            event Action<CollectionCompleteEvent> CollectionComplete;
        }

        public record struct HeapStatsEvent
        {
            public HeapStatsEvent(EventWrittenEventArgs e)
            {
                Gen0SizeBytes = (ulong)e.Payload![0]!;
                Gen1SizeBytes = (ulong)e.Payload![2]!;
                Gen2SizeBytes = (ulong)e.Payload![4]!;
                LohSizeBytes = (ulong)e.Payload![6]!;
                FinalizationQueueLength = (ulong)e.Payload![9]!;
                NumPinnedObjects = (uint)e.Payload![10]!;
#if NET6_0_OR_GREATER
                PohSizeBytes = (ulong)e.Payload![14]!;
#endif
            }

            public ulong Gen0SizeBytes { get; }

            public ulong Gen1SizeBytes { get; }

            public ulong Gen2SizeBytes { get; }

            public ulong LohSizeBytes { get; }

            public ulong FinalizationQueueLength { get; }

            public uint NumPinnedObjects { get; }
#if NET6_0_OR_GREATER
            public ulong PohSizeBytes { get; }
#endif
        }

        public record struct PauseCompleteEvent(TimeSpan PauseDuration);

        public record struct CollectionStartEvent(uint Count, uint Generation, NativeRuntimeEventSource.GCReason Reason);

        public record struct CollectionCompleteEvent(uint Generation, NativeRuntimeEventSource.GCType Type, TimeSpan Duration);
    }
}
