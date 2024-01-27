#if NET
using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class NativeRuntimeEventParser : IEventParser<NativeRuntimeEventParser>,
    NativeEvent.INativeEvent
{
    // flags representing the "Garbage Collection" + "Preparation for garbage collection" pause reasons
    private const uint SuspendGcReasons = 0x1 | 0x6;

    private readonly EventPairTimer<long> _eventPairTimer = new(
        NativeRuntimeEventSource.EventId.ContentionStart,
        NativeRuntimeEventSource.EventId.ContentionStop,
        x => x.OSThreadId);

    private readonly EventPairTimer<uint, GcData> _gcEventTimer = new(
        NativeRuntimeEventSource.EventId.GcStart,
        NativeRuntimeEventSource.EventId.GcStop,
        x => (uint)x.Payload![0]!,
        x => new((uint)x.Payload![1]!, (NativeRuntimeEventSource.GCType)x.Payload![3]!));

    private readonly EventPairTimer<int> _gcPauseEventTimer = new(
        NativeRuntimeEventSource.EventId.SuspendEEStart,
        NativeRuntimeEventSource.EventId.RestartEEStop,
        // Suspensions/ Resumptions are always done sequentially so there is no common value to match events on. Return a constant value as the event id.
        _ => 1);

    public event Action<NativeEvent.ContentionEndEvent>? ContentionEnd;
    public event Action<NativeEvent.HeapStatsEvent>? HeapStats;
    public event Action<NativeEvent.PauseCompleteEvent>? PauseComplete;
    public event Action<NativeEvent.CollectionStartEvent>? CollectionStart;
    public event Action<NativeEvent.CollectionCompleteEvent>? CollectionComplete;
    public event Action<NativeEvent.AllocationTickEvent>? AllocationTick;
    public event Action<NativeEvent.ThreadPoolAdjustedReasonEvent>? ThreadPoolAdjusted;
#if !NET7_0_OR_GREATER
    public event Action<NativeEvent.ThreadPoolAdjustedEvent>? IoThreadPoolAdjusted;
#endif
    public event Action<NativeEvent.ThreadPoolAdjustedEvent>? WorkerThreadPoolAdjusted;

    public string EventSourceName => NativeRuntimeEventSource.Name;

    public EventKeywords Keywords => (EventKeywords)(
        NativeRuntimeEventSource.Keywords.Contention | // thread contention timing
        NativeRuntimeEventSource.Keywords.Exception | // get the first chance
        NativeRuntimeEventSource.Keywords.GC | // garbage collector details
        NativeRuntimeEventSource.Keywords.Threading | // threadpool events
        NativeRuntimeEventSource.Keywords.Type); // for finalizer and exceptions type names

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        switch (e.EventId)
        {
            case NativeRuntimeEventSource.EventId.AllocTick:
                {
                    const uint lohHeapFlag = 0x1;

                    AllocationTick?.Invoke(new((uint)e.Payload![0]!, ((uint)e.Payload![1]! & lohHeapFlag) == lohHeapFlag));

                    return;
                }
            case NativeRuntimeEventSource.EventId.HeapStats:
                HeapStats?.Invoke(new(e));

                return;
            case NativeRuntimeEventSource.EventId.ContentionStart:
            case NativeRuntimeEventSource.EventId.ContentionStop:
                {
                    if (_eventPairTimer.TryGetDuration(e, out var duration) == DurationResult.FinalWithDuration &&
                        duration > TimeSpan.Zero &&
                        (byte)e.Payload![0]! == 0)
                        ContentionEnd?.Invoke(new(duration));

                    return;
                }
            case NativeRuntimeEventSource.EventId.SuspendEEStart:
            case NativeRuntimeEventSource.EventId.RestartEEStop:
                {
                    // Execution engine is pausing for a reason other than GC, discard event.
                    if ((e.EventId != NativeRuntimeEventSource.EventId.SuspendEEStart ||
                         ((uint)e.Payload![0]! & SuspendGcReasons) != 0) &&
                        _gcPauseEventTimer.TryGetDuration(e, out var pauseDuration) == DurationResult.FinalWithDuration &&
                        pauseDuration > TimeSpan.Zero)
                        PauseComplete?.Invoke(new(pauseDuration));

                    return;
                }
            case NativeRuntimeEventSource.EventId.GcStart or NativeRuntimeEventSource.EventId.GcStop:
                switch (_gcEventTimer.TryGetDuration(e, out var gcDuration, out var gcData))
                {
                    case DurationResult.Start:
                        CollectionStart?.Invoke(new((uint)e.Payload![0]!, (uint)e.Payload![1]!,
                            (NativeRuntimeEventSource.GCReason)e.Payload![2]!));
                        break;
                    case DurationResult.FinalWithDuration when gcDuration > TimeSpan.Zero:
                        CollectionComplete?.Invoke(new(gcData.Generation, gcData.Type, gcDuration));
                        break;
                }

                return;
            case NativeRuntimeEventSource.EventId.ThreadPoolAdjustment:
                ThreadPoolAdjusted?.Invoke(new((uint)e.Payload![1]!,
                    (NativeRuntimeEventSource.ThreadAdjustmentReason)e.Payload![2]!));
                return;
#if !NET7_0_OR_GREATER
            case NativeRuntimeEventSource.EventId.IoThreadCreate or NativeRuntimeEventSource.EventId.IoThreadRetire
                or NativeRuntimeEventSource.EventId.IoThreadUnretire
                or NativeRuntimeEventSource.EventId.IoThreadTerminate:
                IoThreadPoolAdjusted?.Invoke(new((uint)e.Payload![0]!, (uint)e.Payload![1]!));
                return;
#endif
            case NativeRuntimeEventSource.EventId.WorkerThreadStart or NativeRuntimeEventSource.EventId.WorkerThreadStop
                or NativeRuntimeEventSource.EventId.WorkerThreadRetirementStart
                or NativeRuntimeEventSource.EventId.WorkerThreadRetirementStop
                or NativeRuntimeEventSource.EventId.WorkerThreadWait:
                WorkerThreadPoolAdjusted?.Invoke(new((uint)e.Payload![0]!, (uint)e.Payload![1]!));
                return;
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
}
#endif
