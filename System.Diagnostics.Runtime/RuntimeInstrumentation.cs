using System.Diagnostics.Metrics;
using System.Diagnostics.Runtime.EventListening;
using System.Diagnostics.Runtime.EventListening.Parsers;
using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace System.Diagnostics.Runtime;

//https://github.com/dotnet/diagnostics/blob/main/src/Tools/dotnet-counters/KnownData.cs
internal class RuntimeInstrumentation : IDisposable
{
    private const string
        LabelType = "type",
        LabelReason = "reason",
        LabelHeap = "heap",
        LabelGeneration = "generation";

    private static readonly Dictionary<NativeRuntimeEventSource.ThreadAdjustmentReason, string> AdjustmentReasonToLabel = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.ThreadAdjustmentReason>();
    private static readonly Dictionary<NativeRuntimeEventSource.GCType, string> GcTypeToLabels = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.GCType>();
    private static readonly Dictionary<NativeRuntimeEventSource.GCReason, string> GcReasonToLabels = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.GCReason>();

    private static readonly string[] GenNames = new[] { "gen0", "gen1", "gen2", "loh", "poh" };
    private static readonly AssemblyName AssemblyName = typeof(RuntimeInstrumentation).Assembly.GetName();
    public static string InstrumentationName { get; } = AssemblyName.Name ?? "System.Diagnostics.Runtime";
    private static readonly string? InstrumentationVersion = AssemblyName.Version?.ToString();
    private readonly IEnumerable<IDisposable> _disposables;

    public RuntimeInstrumentation(RuntimeMetricsOptions options)
    {
        var meter = new Meter(InstrumentationName, InstrumentationVersion);

        var disposables = new List<IDisposable> { meter };

#if NET
        NativeRuntimeEventParser? nativeRuntimeParser = null;

        NativeRuntimeEventParser? CreateNativeRuntimeEventParser()
        {
            if (!options.EnabledNativeRuntime) return null;

            if (nativeRuntimeParser == null)
                disposables.Add(new DotNetEventListener(nativeRuntimeParser = new(), EventLevel.Verbose));

            return nativeRuntimeParser;
        }
#else
        EtwParser? etwParser = null;

        EtwParser? CreateEtwParser()
        {
            if (etwParser != null || string.IsNullOrWhiteSpace(options.EtwSessionName) ||
                !options.EnabledNativeRuntime) return etwParser;

            try
            {
                disposables.Add(etwParser = new(options.EtwSessionName!));
            }
            catch (Exception ex)
            {
                RuntimeEventSource.Log.EtlConstructException(ex);
            }

            return etwParser;
        }
#endif
        if (options.IsContentionEnabled)
#if NETFRAMEWORK
            ContentionInstrumentation(meter, options, CreateEtwParser());
#else
            ContentionInstrumentation(meter, options, CreateNativeRuntimeEventParser());
#endif

        if (options.IsExceptionsEnabled) disposables.Add(ExceptionsInstrumentation(meter, options));
        if (options.IsGcEnabled)
#if NET
            GcInstrumentation(meter, options, CreateNativeRuntimeEventParser());
#else
            GcInstrumentation(meter, options, CreateEtwParser());
#endif
        if (options.IsProcessEnabled) ProcessInstrumentation(meter);

        if (options.IsThreadingEnabled)
        {
#if NETFRAMEWORK
            FrameworkEventParser? frameworkParser = null;

            if (options.EnabledNativeRuntime)
                disposables.Add(new DotNetEventListener(frameworkParser = new(), EventLevel.Verbose));
#endif
            ThreadingInstrumentation(meter, options,
#if NETFRAMEWORK
                frameworkParser, CreateEtwParser());
#else
                CreateNativeRuntimeEventParser());
#endif
        }

        _disposables = disposables;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeyValuePair<string, object?> CreateTag(string key, object? value) => new(key, value);
#if NETFRAMEWORK
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Measurement<T> CreateMeasurement<T>(T value, string tagKey, object? tagValue) where T : struct =>
        new(value, CreateTag(tagKey, tagValue));
#endif
    private static void ContentionInstrumentation(Meter meter, RuntimeMetricsOptions options,
        NativeEvent.INativeEvent? contentionInfo)
    {
        if (contentionInfo == null) return;

        var totalTicks = 0L;
        meter.CreateObservableCounter($"{options.MetricPrefix}monitor.lock_contention.duration",
            () => totalTicks > 0 ? new[] { new Measurement<double>(totalTicks * 100) } : [],
            "ns", "The total amount of time spent contending locks");
#if NETFRAMEWORK
        var contentionTotal = 0L;

        meter.CreateObservableCounter($"{options.MetricPrefix}monitor.lock_contention.count",
            () => contentionTotal > 0 ? new[] { new Measurement<long>(contentionTotal) } : [],
            description: "The number of times there was contention when trying to acquire a monitor lock since the observation started. Monitor locks are commonly acquired by using the lock keyword in C#, or by calling Monitor.Enter() and Monitor.TryEnter().");
#endif
        contentionInfo.ContentionEnd += e =>
        {
            Interlocked.Add(ref totalTicks, e.ContentionDuration.Ticks);
#if NETFRAMEWORK
            Interlocked.Increment(ref contentionTotal);
#endif
        };
    }

    private static IDisposable ExceptionsInstrumentation(Meter meter, RuntimeMetricsOptions options)
    {
        var exceptionTypesCounter = meter.CreateCounter<long>(
            $"{options.MetricPrefix}exception_types.count",
            description: "Count of exception types that have been thrown in managed code, since the observation started. The value will be unavailable until an exception has been thrown after System.Diagnostics.Runtime initialization.");

        // ReSharper disable once ConvertToLocalFunction
        EventHandler<FirstChanceExceptionEventArgs> firstChanceException = (_, args) =>
        {
            try
            {
                var type = args.Exception.GetType();

                if (type.IsGenericType) type = type.GetGenericTypeDefinition();

                exceptionTypesCounter.Add(1, CreateTag(LabelType, type.FullName ?? type.Name));
            }
            catch
            {
                // ignored
            }
        };

        AppDomain.CurrentDomain.FirstChanceException += firstChanceException;

        return new DisposableAction(() => AppDomain.CurrentDomain.FirstChanceException -= firstChanceException);
    }

    private static void GcInstrumentation(Meter meter, RuntimeMetricsOptions options,
#if NET
        NativeEvent.INativeEvent? nativeEvent)
#else
        NativeEvent.IExtendNativeEvent? nativeEvent)
#endif
    {
#if NET
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.available_memory.size",
            () => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            "bytes", "The total available memory, in bytes, for the garbage collector to use when the last garbage collection occurred.");
#endif
        if (nativeEvent == null) return;
#if NETFRAMEWORK
        var gcDuration = 0L;
        meter.CreateObservableCounter($"{options.MetricPrefix}gc.duration",
            () => gcDuration == default ? [] : new[] { new Measurement<long>(Interlocked.Read(ref gcDuration) * 100) },
            "ns", "The total amount of time paused in GC since the process start.");

        nativeEvent.CollectionComplete += e => Interlocked.Add(ref gcDuration, e.Duration.Ticks);
#endif
        var gcPauseDuration = 0L;
        meter.CreateObservableCounter($"{options.MetricPrefix}gc.pause.duration",
            () => gcPauseDuration == default ? [] : new[] { new Measurement<long>(Interlocked.Read(ref gcPauseDuration) * 100) },
            "ns", "The amount of time execution was paused for garbage collections");

        nativeEvent.PauseComplete += e => Interlocked.Add(ref gcPauseDuration, e.PauseDuration.Ticks);

        var gcCollections = meter.CreateCounter<int>($"{options.MetricPrefix}gc.reasons.count",
            description: "Count the number of garbage collection reasons that have occurred since the observation started. The value will be unavailable until GC has been collection after System.Diagnostics.Runtime initialization.");

        nativeEvent.CollectionStart += e =>
        {
            if (e.Generation >= 0 && e.Generation < GenNames.Length)
                gcCollections.Add(1,
                    CreateTag(LabelGeneration, GenNames[e.Generation]),
                    CreateTag(LabelReason, GcReasonToLabels[e.Reason]),
                    CreateTag(LabelType, GcTypeToLabels[e.Type]));
        };

        NativeEvent.HeapStatsEvent stats = default;
        nativeEvent.HeapStats += e => stats = e;
#if NETFRAMEWORK
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.heap.size", () => stats == default
            ? []
            : new[]
            {
                CreateMeasurement(stats.Gen0SizeBytes, LabelGeneration, "gen0"),
                CreateMeasurement(stats.Gen1SizeBytes, LabelGeneration, "gen1"),
                CreateMeasurement(stats.Gen2SizeBytes, LabelGeneration, "gen2"),
                CreateMeasurement(stats.LohSizeBytes, LabelGeneration, "loh")
            }, "bytes", "Count of bytes currently in use by objects in the GC heap that haven't been collected yet. Fragmentation and other GC committed memory pools are excluded.");
#endif
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.pinned.objects",
            () => stats == default ? [] : new[] { new Measurement<int>(stats.NumPinnedObjects) },
            description: "The number of pinned objects");

        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.finalization.queue.length",
            () => stats == default ? [] : new[] { new Measurement<long>(stats.FinalizationQueueLength) },
            description: "The number of objects waiting to be finalized");
#if NETFRAMEWORK
        var fragmentedBytes = -1L;

        nativeEvent.HeapFragmentation += e =>
        {
            if (fragmentedBytes >= 0 || e.FragmentedBytes > 0)
                fragmentedBytes = e.FragmentedBytes;
        };

        meter.CreateObservableGauge($"{options.MetricPrefix}gc.heap.fragmentation.size", () =>
                GetFragmentation(fragmentedBytes, stats.Gen0SizeBytes + stats.Gen1SizeBytes + stats.Gen2SizeBytes + stats.LohSizeBytes),
            description: "The heap fragmentation, as observed during the latest garbage collection. The value will be unavailable until at least one garbage collection has occurred.");
#endif
        var loh = 0L;
        var soh = 0L;

        meter.CreateObservableCounter($"{options.MetricPrefix}gc.heap_allocations.size", () =>
        {
            var list=  new List<Measurement<long>>();

            if (loh > 0) list.Add(new(loh, CreateTag(LabelHeap, "loh")));

            if (soh > 0) list.Add(new(soh, CreateTag(LabelHeap, "soh")));

            return list;
        }, "bytes", "Count of bytes allocated on the managed GC heap since the observation started. .NET objects are allocated from this heap. Object allocations from unmanaged languages such as C/C++ do not use this heap.");

        nativeEvent.AllocationTick += e => Interlocked.Add(ref e.IsLargeObjectHeap ? ref loh : ref soh,
            e.AllocatedBytes);
    }
#if NETFRAMEWORK
    private static IEnumerable<Measurement<double>> GetFragmentation(long fragmentedBytes, long heapSizeBytes) =>
        fragmentedBytes < 0 || heapSizeBytes <= 0
            ? []
            : new[] { new Measurement<double>(fragmentedBytes * 100d / heapSizeBytes) };
#endif
    private static void ProcessInstrumentation(Meter meter)
    {
        meter.CreateObservableGauge<double>("process.cpu.utilization",
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? CpuUtilization.GetCpuUsage
                : new(ProcessTimes.GetCpuUsage),
            description: "CPU utilization");

        meter.CreateObservableUpDownCounter("process.handle.count", () => Process.GetCurrentProcess().HandleCount, description: "Process handle count");
    }

    private static void ThreadingInstrumentation(Meter meter, RuntimeMetricsOptions options,
#if NETFRAMEWORK
        FrameworkEventParser.Events.Verbose? frameworkVerbose,
#endif
        NativeEvent.INativeEvent? nativeEvent)
    {
#if NETFRAMEWORK
        if (frameworkVerbose != null)
        {
            var completedItems = 0L;
            var total = 0L;

            frameworkVerbose.Enqueue += () => Interlocked.Increment(ref total);
            frameworkVerbose.Dequeue += () =>
            {
                Interlocked.Increment(ref completedItems);
                if (Interlocked.Read(ref total) > 0) Interlocked.Decrement(ref total);
            };

            meter.CreateObservableCounter($"{options.MetricPrefix}thread_pool.completed_items.count",
                () => completedItems,
                 description: "The number of work items that have been processed by the thread pool since the observation started.");

            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}thread_pool.queue.length",
                () => Math.Max(0, total),
                 description: "The number of work items that are currently queued to be processed by the thread pool.");
        }
#endif
        if (nativeEvent == null) return;

        var adjustmentsTotal = meter.CreateCounter<int>($"{options.MetricPrefix}thread_pool.adjustments.count",
            description:
            "The total number of changes made to the size of the thread pool, labeled by the reason for change");
#if NETFRAMEWORK
        var threadCount = -1;

        nativeEvent.ThreadPoolAdjusted += e =>
        {
            threadCount = (int)e.NumThreads;

            adjustmentsTotal.Add(1, CreateTag(LabelReason, AdjustmentReasonToLabel[e.AdjustmentReason]));
        };

        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}thread_pool.threads.count",
            () => threadCount < 0 ? [] : new[] { new Measurement<int>(threadCount) },
            description: "The number of thread pool threads that currently exist.");

        nativeEvent.WorkerThreadPoolAdjusted += RegisterThreadPool(meter, options, "worker");

        // IO threadpool only exists on windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            nativeEvent.IoThreadPoolAdjusted += RegisterThreadPool(meter, options, "io");
#else
        nativeEvent.ThreadPoolAdjusted += e =>
            adjustmentsTotal.Add(1, CreateTag(LabelReason, AdjustmentReasonToLabel[e.AdjustmentReason]));
#endif
    }
#if NETFRAMEWORK
    private static Action<NativeEvent.ThreadPoolAdjustedEvent> RegisterThreadPool(Meter meter, RuntimeMetricsOptions options,
        string threadType)
    {
        var activeThreads = -1L;
        var retiredThreads = -1L;

        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}thread_pool.active.{threadType}.threads.count",
            () => activeThreads < 0 ? [] : new[] { new Measurement<long>(activeThreads) },
            description: $"The number of active {threadType} threads");

        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}thread_pool.{threadType}.threads.count",
            () => retiredThreads < 0
                ? []
                : new[] { new Measurement<long>(Math.Max(0, activeThreads) + retiredThreads) },
            description: $"The number of {threadType} threads");

        return e => (activeThreads, retiredThreads) = e;
    }
#endif
    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }

    private class DisposableAction(Action action) : IDisposable
    {
        public void Dispose() => Interlocked.Exchange(ref action!, null)?.Invoke();
    }
}
