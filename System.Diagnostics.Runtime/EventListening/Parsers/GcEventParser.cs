using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class GcEventParser : IEventParser<GcEventParser>, GcEventParser.Events.Info, GcEventParser.Events.Verbose
{
    private readonly EventPairTimer<uint, GcData> _gcEventTimer = new(
        NativeRuntimeEventSource.EventId.GcStart,
        NativeRuntimeEventSource.EventId.GcStop,
        x => (uint)x.Payload![0]!,
        x => new GcData((uint)x.Payload![1]!, (NativeRuntimeEventSource.GCType)x.Payload![3]!));

    private readonly EventPairTimer<int> _gcPauseEventTimer = new(
        NativeRuntimeEventSource.EventId.SuspendEEStart,
        NativeRuntimeEventSource.EventId.RestartEEStop,
        // Suspensions/ Resumptions are always done sequentially so there is no common value to match events on. Return a constant value as the event id.
        x => 1);

    public event Action<Events.HeapStatsEvent>? HeapStats;
    public event Action<Events.PauseCompleteEvent>? PauseComplete;
    public event Action<Events.CollectionStartEvent>? CollectionStart;
    public event Action<Events.CollectionCompleteEvent>? CollectionComplete;
    public event Action<Events.AllocationTickEvent>? AllocationTick;

    public string EventSourceName => NativeRuntimeEventSource.Name;
    public EventKeywords Keywords => (EventKeywords)NativeRuntimeEventSource.Keywords.GC;

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (e.EventId == NativeRuntimeEventSource.EventId.AllocTick)
        {
            const uint lohHeapFlag = 0x1;

            AllocationTick?.Invoke(new((uint)e.Payload![0]!, ((uint)e.Payload![1]! & lohHeapFlag) == lohHeapFlag));

            return;
        }

        if (e.EventId == NativeRuntimeEventSource.EventId.HeapStats)
        {
            HeapStats?.Invoke(new(e));

            return;
        }

        // flags representing the "Garbage Collection" + "Preparation for garbage collection" pause reasons
        const uint suspendGcReasons = 0x1 | 0x6;

        if (e.EventId == NativeRuntimeEventSource.EventId.SuspendEEStart && ((uint)e.Payload![0]! & suspendGcReasons) == 0)
            return; // Execution engine is pausing for a reason other than GC, discard event.

        if (_gcPauseEventTimer.TryGetDuration(e, out var pauseDuration) == DurationResult.FinalWithDuration && pauseDuration > TimeSpan.Zero)
        {
            PauseComplete?.Invoke(new(pauseDuration));
            return;
        }

        switch (_gcEventTimer.TryGetDuration(e, out var gcDuration, out var gcData))
        {
            case DurationResult.Start:
                CollectionStart?.Invoke(new((uint)e.Payload![0]!, (uint)e.Payload![1]!, (NativeRuntimeEventSource.GCReason)e.Payload![2]!));
                break;
            case DurationResult.FinalWithDuration when gcDuration > TimeSpan.Zero:
                CollectionComplete?.Invoke(new(gcData.Generation, gcData.Type, gcDuration));
                break;
        }
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

        public interface Verbose : IVerboseEvents
        {
            event Action<AllocationTickEvent> AllocationTick;
        }

        public record struct HeapStatsEvent
        {
            public HeapStatsEvent(EventWrittenEventArgs e)
            {
                Gen0SizeBytes = (long)(ulong)e.Payload![0]!;
                Gen1SizeBytes = (long)(ulong)e.Payload![2]!;
                Gen2SizeBytes = (long)(ulong)e.Payload![4]!;
                LohSizeBytes = (long)(ulong)e.Payload![6]!;
                FinalizationQueueLength = (long)(ulong)e.Payload![9]!;
                NumPinnedObjects = (int)(uint)e.Payload![10]!;
#if NET6_0_OR_GREATER
                PohSizeBytes = (long)(ulong)e.Payload![14]!;
#endif
            }
#if NETFRAMEWORK
            public HeapStatsEvent(Microsoft.Diagnostics.Tracing.Parsers.Clr.GCHeapStatsTraceData data)
            {
                Gen0SizeBytes = data.GenerationSize0;
                Gen1SizeBytes = data.GenerationSize1;
                Gen2SizeBytes = data.GenerationSize2;
                LohSizeBytes = data.GenerationSize3;
                FinalizationQueueLength = data.FinalizationPromotedCount;
                NumPinnedObjects = data.PinnedObjectCount;
            }
#endif
            public long Gen0SizeBytes { get; }

            public long Gen1SizeBytes { get; }

            public long Gen2SizeBytes { get; }

            public long LohSizeBytes { get; }

            public long FinalizationQueueLength { get; }

            public int NumPinnedObjects { get; }
#if NET6_0_OR_GREATER
            public long PohSizeBytes { get; }
#endif
        }

        public record struct PauseCompleteEvent(TimeSpan PauseDuration);

        public record struct CollectionStartEvent(uint Count, uint Generation, NativeRuntimeEventSource.GCReason Reason);

        public record struct CollectionCompleteEvent(uint Generation, NativeRuntimeEventSource.GCType Type, TimeSpan Duration);

        public record struct AllocationTickEvent(uint AllocatedBytes, bool IsLargeObjectHeap);
    }
}
