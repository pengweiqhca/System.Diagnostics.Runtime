using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening;

public static class NativeEvent
{
    public interface INativeEvent
    {
        event Action<ContentionEndEvent> ContentionEnd;
        event Action<HeapStatsEvent> HeapStats;
#if NETFRAMEWORK
        event Action<PauseCompleteEvent> PauseComplete;
#endif
        event Action<CollectionStartEvent> CollectionStart;
        event Action<CollectionCompleteEvent> CollectionComplete;
        event Action<AllocationTickEvent> AllocationTick;
        event Action<ThreadPoolAdjustedReasonEvent> ThreadPoolAdjusted;
#if NETFRAMEWORK
        event Action<ThreadPoolAdjustedEvent> IoThreadPoolAdjusted;
        event Action<ThreadPoolAdjustedEvent> WorkerThreadPoolAdjusted;
#endif
    }

    public interface IExtendNativeEvent : INativeEvent
    {
        event Action<HeapFragmentationEvent> HeapFragmentation;
    }

    public record struct ContentionEndEvent(TimeSpan ContentionDuration);

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
#if NET
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
#if NET
        public long PohSizeBytes { get; }
#endif
    }

    public record struct PauseCompleteEvent(TimeSpan PauseDuration);

    public record struct CollectionStartEvent(
        long Generation,
        NativeRuntimeEventSource.GCReason Reason,
        NativeRuntimeEventSource.GCType Type);

    public record struct CollectionCompleteEvent(long Generation, TimeSpan Duration);

    public record struct AllocationTickEvent(long AllocatedBytes, bool IsLargeObjectHeap);

    public record struct HeapFragmentationEvent(long FragmentedBytes);

    public record struct ThreadPoolAdjustedReasonEvent(
        long NumThreads,
        NativeRuntimeEventSource.ThreadAdjustmentReason AdjustmentReason);

    public record struct ThreadPoolAdjustedEvent(long ActiveNumThreads, long RetiredNumThreads);
}
