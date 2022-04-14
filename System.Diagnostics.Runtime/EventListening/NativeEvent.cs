using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening;

public static class NativeEvent
{
    public interface Error : IInfoEvents
    {
        event Action<ExceptionThrownEvent> ExceptionThrown;
    }

    public interface Info : IInfoEvents
    {
        event Action<ContentionEndEvent> ContentionEnd;
        event Action<HeapStatsEvent> HeapStats;
        event Action<PauseCompleteEvent> PauseComplete;
        event Action<CollectionStartEvent> CollectionStart;
        event Action<CollectionCompleteEvent> CollectionComplete;
        event Action<ThreadPoolAdjustedEvent> ThreadPoolAdjusted;
        event Action<IoThreadPoolAdjustedEvent> IoThreadPoolAdjusted;
    }

    public interface Verbose : IVerboseEvents
    {
        event Action<AllocationTickEvent> AllocationTick;
    }

    public record struct ContentionEndEvent(TimeSpan ContentionDuration);

    public record struct ExceptionThrownEvent(string? ExceptionType);

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

    public record struct ThreadPoolAdjustedEvent(uint NumThreads, NativeRuntimeEventSource.ThreadAdjustmentReason AdjustmentReason);

    public record struct IoThreadPoolAdjustedEvent(uint NumThreads);
}
