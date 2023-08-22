#if NET
using System.Diagnostics.Runtime.EventListening.Sources;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

//https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/RuntimeEventSource.cs#L78
public class SystemRuntimeEventParser : EventCounterParserBase<SystemRuntimeEventParser>,
    SystemRuntimeEventParser.Events.Counters
{
#pragma warning disable CS0067
    [CounterName("cpu-usage")] public event Action<MeanCounterValue>? CpuUsage;

    [CounterName("working-set")] public event Action<MeanCounterValue>? WorkingSet;

    [CounterName("gc-heap-size")] public event Action<MeanCounterValue>? GcHeapSize;

    [CounterName("gen-0-gc-count")] public event Action<IncrementingCounterValue>? Gen0GcCount;

    [CounterName("gen-1-gc-count")] public event Action<IncrementingCounterValue>? Gen1GcCount;

    [CounterName("gen-2-gc-count")] public event Action<IncrementingCounterValue>? Gen2GcCount;

    [CounterName("threadpool-thread-count")]
    public event Action<MeanCounterValue>? ThreadPoolThreadCount;

    [CounterName("monitor-lock-contention-count")]
    public event Action<IncrementingCounterValue>? MonitorLockContentionCount;

    [CounterName("threadpool-queue-length")]
    public event Action<MeanCounterValue>? ThreadPoolQueueLength;

    [CounterName("threadpool-completed-items-count")]
    public event Action<IncrementingCounterValue>? ThreadPoolCompletedItemsCount;

    [CounterName("alloc-rate")] public event Action<IncrementingCounterValue>? AllocRate;

    [CounterName("active-timer-count")] public event Action<MeanCounterValue>? ActiveTimerCount;

    [CounterName("exception-count")] public event Action<IncrementingCounterValue>? ExceptionCount;

    [CounterName("time-in-gc")] public event Action<MeanCounterValue>? TimeInGc;

    [CounterName("gen-0-size")] public event Action<MeanCounterValue>? Gen0Size;

    [CounterName("gen-1-size")] public event Action<MeanCounterValue>? Gen1Size;

    [CounterName("gen-2-size")] public event Action<MeanCounterValue>? Gen2Size;

    [CounterName("loh-size")] public event Action<MeanCounterValue>? LohSize;

    [CounterName("assembly-count")] public event Action<MeanCounterValue>? NumAssembliesLoaded;

    [CounterName("gc-fragmentation")] public event Action<MeanCounterValue>? GcFragmentation;

    [CounterName("poh-size")] public event Action<MeanCounterValue>? PohSize;

    [CounterName("il-bytes-jitted")] public event Action<MeanCounterValue>? IlBytesJitted;

    [CounterName("methods-jitted-count")] public event Action<MeanCounterValue>? MethodsJittedCount;

    [CounterName("time-in-jit")] public event Action<IncrementingCounterValue>? TimeInJit;
#pragma warning restore CS0067

    public override string EventSourceName => SystemRuntimeEventSource.Name;

    public static class Events
    {
        public interface Counters : ICounterEvents
        {
            event Action<MeanCounterValue>? CpuUsage;
            event Action<MeanCounterValue>? WorkingSet;
            event Action<MeanCounterValue> GcHeapSize;
            event Action<IncrementingCounterValue> Gen0GcCount;
            event Action<IncrementingCounterValue> Gen1GcCount;
            event Action<IncrementingCounterValue> Gen2GcCount;
            event Action<MeanCounterValue> ThreadPoolThreadCount;
            event Action<IncrementingCounterValue> MonitorLockContentionCount;
            event Action<MeanCounterValue> ThreadPoolQueueLength;
            event Action<IncrementingCounterValue> ThreadPoolCompletedItemsCount;
            event Action<IncrementingCounterValue> AllocRate;
            event Action<MeanCounterValue> ActiveTimerCount;
            event Action<IncrementingCounterValue> ExceptionCount;
            event Action<MeanCounterValue> TimeInGc;
            event Action<MeanCounterValue> Gen0Size;
            event Action<MeanCounterValue> Gen1Size;
            event Action<MeanCounterValue> Gen2Size;
            event Action<MeanCounterValue> LohSize;
            event Action<MeanCounterValue> NumAssembliesLoaded;

            event Action<MeanCounterValue> GcFragmentation;
            event Action<MeanCounterValue> PohSize;
            event Action<MeanCounterValue> IlBytesJitted;
            event Action<MeanCounterValue> MethodsJittedCount;
            event Action<IncrementingCounterValue> TimeInJit;
        }
    }
}
#endif
