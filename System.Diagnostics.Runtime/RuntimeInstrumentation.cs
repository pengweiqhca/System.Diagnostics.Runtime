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
public class RuntimeInstrumentation : IDisposable
{
    private const string
        LabelAdjustmentReason = "adjustment.reason",
        LabelType = "type",
        LabelState = "state",
        LabelReason = "gc.reason",
        LabelGcType = "gc.type",
        LabelHeap = "gc.heap",
        LabelGeneration = "gc.generation";

    private static readonly Dictionary<NativeRuntimeEventSource.ThreadAdjustmentReason, string> AdjustmentReasonToLabel = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.ThreadAdjustmentReason>();
    private static readonly Dictionary<NativeRuntimeEventSource.GCType, string> GcTypeToLabels = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.GCType>();
    private static readonly Dictionary<NativeRuntimeEventSource.GCReason, string> GcReasonToLabels = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.GCReason>();

    private static readonly AssemblyName AssemblyName = typeof(RuntimeInstrumentation).Assembly.GetName();
    public static string InstrumentationName { get; } = AssemblyName.Name ?? "System.Diagnostics.Runtime";
    private static readonly string? InstrumentationVersion = AssemblyName.Version?.ToString();
    private readonly IEnumerable<IDisposable> _disposables;

    public RuntimeInstrumentation(RuntimeMetricsOptions options)
    {
        var meter = new Meter(InstrumentationName, InstrumentationVersion);

        var disposables = new List<IDisposable> { meter };

        if (options.IsAssembliesEnabled) AssembliesInstrumentation(meter, options);
#if NET
        NativeRuntimeEventParser? nativeRuntimeParser = null;
        SystemRuntimeEventParser? systemRuntimeParser = null;

        NativeRuntimeEventParser? CreateNativeRuntimeEventParser()
        {
            if (!options.EnabledNativeRuntime) return null;

            if (nativeRuntimeParser == null)
                disposables.Add(new DotNetEventListener(nativeRuntimeParser = new(), EventLevel.Verbose));

            return nativeRuntimeParser;
        }

        SystemRuntimeEventParser CreateSystemRuntimeEventParser()
        {
            if (systemRuntimeParser == null)
                disposables.Add(new DotNetEventListener(systemRuntimeParser = new(), EventLevel.LogAlways));

            return systemRuntimeParser;
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

        if (options.IsDnsEnabled)
        {
            var parser = new NameResolutionEventParser();

            disposables.Add(new DotNetEventListener(parser, EventLevel.LogAlways));

            DnsInstrumentation(meter, options, parser);
        }
#endif

        if (options.IsExceptionsEnabled) disposables.Add(ExceptionsInstrumentation(meter, options));
        if (options.IsGcEnabled)
#if NET
            GcInstrumentation(meter, options, CreateSystemRuntimeEventParser(), CreateNativeRuntimeEventParser());
#else
            GcInstrumentation(meter, options, CreateEtwParser());
#endif
#if NET
        if (options.IsJitEnabled) JitInstrumentation(meter, options);
#endif
        if (options.IsProcessEnabled) ProcessInstrumentation(meter);
#if NET
        if (options.IsSocketsEnabled)
        {
            var parser = new SocketsEventParser();

            disposables.Add(new DotNetEventListener(parser, EventLevel.LogAlways));

            SocketsInstrumentation(meter, options, parser);
        }
#endif
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Measurement<T> CreateMeasurement<T>(T value, string tagKey, object? tagValue) where T : struct =>
        new(value, CreateTag(tagKey, tagValue));

    private static void AssembliesInstrumentation(Meter meter, RuntimeMetricsOptions options) =>
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}assembly.count",
            () => AppDomain.CurrentDomain.GetAssemblies().Length, description: "Number of Assemblies Loaded");

    private static void ContentionInstrumentation(Meter meter, RuntimeMetricsOptions options,
        NativeEvent.INativeEvent? contentionInfo)
    {
#if NET
        meter.CreateObservableCounter($"{options.MetricPrefix}lock.contention.total", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
#endif
        if (contentionInfo == null) return;

        var contentionSecondsTotal = meter.CreateCounter<double>($"{options.MetricPrefix}lock.contention.time.total", "s", "The total amount of time spent contending locks");
#if NETFRAMEWORK
        var contentionTotal = meter.CreateCounter<long>($"{options.MetricPrefix}lock.contention.total", description: "Monitor Lock Contention Count");
        contentionInfo.ContentionEnd += e =>
        {
            contentionSecondsTotal.Add(e.ContentionDuration.TotalSeconds);

            contentionTotal.Add(1);
        };
#else
        contentionInfo.ContentionEnd += e => contentionSecondsTotal.Add(e.ContentionDuration.TotalSeconds);
#endif
    }
#if NET
    private static void DnsInstrumentation(Meter meter, RuntimeMetricsOptions options,
        NameResolutionEventParser.Events.CountersV5_0? nameResolutionCounters)
    {
        if (nameResolutionCounters == null) return;

        var dnsLookupsRequested = -1L;
        nameResolutionCounters.DnsLookupsRequested += e => dnsLookupsRequested = (long)e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}dns.requested.total",
            () => dnsLookupsRequested < 0
                ? Array.Empty<Measurement<long>>()
                : new[] { new Measurement<long>(dnsLookupsRequested) },
            description: "The total number of dns lookup requests");

        var currentDnsLookups = -1L;
        nameResolutionCounters.CurrentDnsLookups += e => currentDnsLookups = (long)e.Mean;
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}dns.current.count",
            () => currentDnsLookups < 0 ? Array.Empty<Measurement<long>>() : new[] { new Measurement<long>(currentDnsLookups) },
            description: "The total number of current dns lookups");

        var dnsLookupsDuration = -1d;
        nameResolutionCounters.DnsLookupsDuration += e => dnsLookupsDuration = e.Total;
        meter.CreateObservableCounter($"{options.MetricPrefix}dns.duration.total",
            () => dnsLookupsDuration < 0
                ? Array.Empty<Measurement<double>>()
                : new[] { new Measurement<double>(dnsLookupsDuration) },
            "ms", "The sum of dns lookup durations");
    }
#endif
    private static IDisposable ExceptionsInstrumentation(Meter meter, RuntimeMetricsOptions options)
    {
        var exceptionCounter = meter.CreateCounter<long>(
            $"{options.MetricPrefix}exception.total",
            description: "Count of exceptions that have been thrown in managed code, since the observation started. The value will be unavailable until an exception has been thrown after OpenTelemetry.Instrumentation.Runtime initialization.");

        // ReSharper disable once ConvertToLocalFunction
        EventHandler<FirstChanceExceptionEventArgs> firstChanceException = (_, args) =>
        {
            try
            {
                var type = args.Exception.GetType();

                exceptionCounter.Add(1, CreateTag(LabelType, type.FullName ?? type.Name));
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
        SystemRuntimeEventParser.Events.CountersV5_0? runtimeCounters,
        NativeEvent.INativeEvent? nativeEvent)
#else
        NativeEvent.IExtendNativeEvent? nativeEvent)
#endif
    {
#if NET
        meter.CreateObservableGauge($"{options.MetricPrefix}gc.fragmentation", GetFragmentation, "%", "GC fragmentation");
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.memory.total.available",
            () => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            "B", "The upper limit on the amount of physical memory .NET can allocate to");

        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.committed.total", () => GC.GetGCMemoryInfo().TotalCommittedBytes, "B", description: "GC Committed bytes since process start");

        TimeInGc(meter, options, runtimeCounters);
#endif
        if (nativeEvent != null)
        {
            var gcCollectionSeconds = meter.CreateHistogram<double>(
                $"{options.MetricPrefix}gc.collection.time", "ms",
                "The amount of time spent running garbage collections");

            nativeEvent.CollectionComplete += e => gcCollectionSeconds.Record(e.Duration.TotalMilliseconds,
                CreateTag(LabelGeneration, GetGenerationToString(e.Generation)),
                CreateTag(LabelGcType, GcTypeToLabels[e.Type]));

            var gcPauseSeconds = meter.CreateHistogram<double>(
                $"{options.MetricPrefix}gc.pause.time", "ms",
                "The amount of time execution was paused for garbage collection");

            nativeEvent.PauseComplete += e => gcPauseSeconds.Record(e.PauseDuration.TotalMilliseconds);

            var gcCollections = meter.CreateCounter<int>(
                $"{options.MetricPrefix}gc.collection.total",
                description: "Counts the number of garbage collections that have occurred, broken down by generation number and the reason for the collection.");

            nativeEvent.CollectionStart += e => gcCollections.Add(1,
                CreateTag(LabelGeneration, GetGenerationToString(e.Generation)),
                CreateTag(LabelReason, GcReasonToLabels[e.Reason]));

            NativeEvent.HeapStatsEvent stats = default;
            nativeEvent.HeapStats += e => stats = e;

            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.heap.size", () => stats == default
                ? Array.Empty<Measurement<long>>()
                : new[]
                {
                    CreateMeasurement(stats.Gen0SizeBytes, LabelGeneration, "0"),
                    CreateMeasurement(stats.Gen1SizeBytes, LabelGeneration, "1"),
                    CreateMeasurement(stats.Gen2SizeBytes, LabelGeneration, "2"),
                    CreateMeasurement(stats.LohSizeBytes, LabelGeneration, "loh")
#if NET
                    , CreateMeasurement(stats.PohSizeBytes, LabelGeneration, "poh")
#endif
                }, "B", "The current size of all heaps (only updated after a garbage collection)");

            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.pinned.objects",
                () => stats == default ? Array.Empty<Measurement<int>>() : new[] { new Measurement<int>(stats.NumPinnedObjects) },
                description: "The number of pinned objects");

            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.finalization.queue.length",
                () => stats == default ? Array.Empty<Measurement<long>>() : new[] { new Measurement<long>(stats.FinalizationQueueLength) },
                description: "The number of objects waiting to be finalized");
#if NETFRAMEWORK
            var fragmentedBytes = -1L;

            nativeEvent.HeapFragmentation += e =>
            {
                if (fragmentedBytes >= 0 || e.FragmentedBytes > 0)
                    fragmentedBytes = e.FragmentedBytes;
            };

            meter.CreateObservableGauge($"{options.MetricPrefix}gc.fragmentation", () =>
                    GetFragmentation(fragmentedBytes, stats.Gen0SizeBytes + stats.Gen1SizeBytes + stats.Gen2SizeBytes + stats.LohSizeBytes),
                "%", "GC fragmentation");
#endif
            var allocated = meter.CreateCounter<long>($"{options.MetricPrefix}gc.allocated.total", "B", "Allocation bytes since process start");

            nativeEvent.AllocationTick += e => allocated.Add(e.AllocatedBytes, CreateTag(LabelHeap, e.IsLargeObjectHeap ? "loh" : "soh"));
        }
        else
        {
            meter.CreateObservableCounter($"{options.MetricPrefix}gc.collection.total", () => new[]
            {
                CreateMeasurement(GC.CollectionCount(0), LabelGeneration, "0"),
                CreateMeasurement(GC.CollectionCount(1), LabelGeneration, "1"),
                CreateMeasurement(GC.CollectionCount(2), LabelGeneration, "2")
            }, description: "Counts the number of garbage collections that have occurred");
#if NET
            meter.CreateObservableCounter($"{options.MetricPrefix}gc.allocated.total", () => GC.GetTotalAllocatedBytes(), "B", "Allocation bytes since process start");

            if (runtimeCounters != null)
            {
                var heapSize = new HeapSize();

                meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.heap.size",
                    () => heapSize.Gen0SizeBytes > 0
                        ? new[]
                        {
                            CreateMeasurement(heapSize.Gen0SizeBytes, LabelGeneration, "0"),
                            CreateMeasurement(heapSize.Gen1SizeBytes, LabelGeneration, "1"),
                            CreateMeasurement(heapSize.Gen2SizeBytes, LabelGeneration, "2"),
                            CreateMeasurement(heapSize.LohSizeBytes, LabelGeneration, "loh"),
                            CreateMeasurement(heapSize.PohSizeBytes, LabelGeneration, "poh")
                        }
                        : Array.Empty<Measurement<long>>(),
                    "B", "The current size of all heaps (only updated after a garbage collection)");

                runtimeCounters.Gen0Size += e => heapSize.Gen0SizeBytes = (long)e.Mean;
                runtimeCounters.Gen1Size += e => heapSize.Gen1SizeBytes = (long)e.Mean;
                runtimeCounters.Gen2Size += e => heapSize.Gen2SizeBytes = (long)e.Mean;
                runtimeCounters.LohSize += e => heapSize.LohSizeBytes = (long)e.Mean;
                runtimeCounters.PohSize += e => heapSize.PohSizeBytes = (long)e.Mean;
            }
            else if (Environment.Version.Major > 6)
                meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.heap.size",
                    () =>
                    {
                        var generationInfo = GC.GetGCMemoryInfo().GenerationInfo;

                        return new[]
                        {
                            CreateMeasurement(generationInfo[0].SizeAfterBytes, LabelGeneration, "0"),
                            CreateMeasurement(generationInfo[1].SizeAfterBytes, LabelGeneration, "1"),
                            CreateMeasurement(generationInfo[2].SizeAfterBytes, LabelGeneration, "2"),
                            CreateMeasurement(generationInfo[3].SizeAfterBytes, LabelGeneration, "loh"),
                            CreateMeasurement(generationInfo[4].SizeAfterBytes, LabelGeneration, "poh")
                        };
                    },
                    "B", "The current size of all heaps (only updated after a garbage collection)");
            else if (typeof(GC).GetMethod("GetGenerationSize", BindingFlags.Static | BindingFlags.NonPublic)?
                         .CreateDelegate(typeof(Func<int, ulong>)) is Func<int, ulong> func)
                meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.heap.size",
                    () => new[]
                    {
                        CreateMeasurement((long)func(0), LabelGeneration, "0"),
                        CreateMeasurement((long)func(1), LabelGeneration, "1"),
                        CreateMeasurement((long)func(2), LabelGeneration, "2"),
                        CreateMeasurement((long)func(3), LabelGeneration, "loh"),
                        CreateMeasurement((long)func(4), LabelGeneration, "poh")
                    },
                    "B", "The current size of all heaps (only updated after a garbage collection)");
            else
#endif
            {
                meter.CreateObservableUpDownCounter($"{options.MetricPrefix}gc.heap.size", () => GC.GetTotalMemory(false), "B", "The current size of all heaps");
            }
        }
    }

    private static string GetGenerationToString(uint generation) => generation switch
    {
        0 => "0",
        1 => "1",
        2 => "2",
        // large object heap
        3 => "loh",
        // pinned object heap, .NET 5+ only
        4 => "poh",
        _ => generation.ToString()
    };

    private static IEnumerable<Measurement<double>> GetFragmentation(long fragmentedBytes, long heapSizeBytes) =>
        fragmentedBytes < 0 || heapSizeBytes <= 0
            ? Array.Empty<Measurement<double>>()
            : new[] { new Measurement<double>(fragmentedBytes * 100d / heapSizeBytes) };
#if NET
    private static IEnumerable<Measurement<double>> GetFragmentation()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        return GetFragmentation(gcInfo.FragmentedBytes, gcInfo.HeapSizeBytes);
    }

    private class HeapSize
    {
        public long Gen0SizeBytes { get; set; }
        public long Gen1SizeBytes { get; set; }
        public long Gen2SizeBytes { get; set; }
        public long LohSizeBytes { get; set; }
        public long PohSizeBytes { get; set; }
    }

    private static void TimeInGc(Meter meter, RuntimeMetricsOptions options,
        SystemRuntimeEventParser.Events.CountersV3_0? runtimeCounters)
    {
        Func<int>? timeInGc;
        if (runtimeCounters != null)
        {
            MeanCounterValue gcPause = default;

            runtimeCounters.TimeInGc += e => gcPause = e;

            timeInGc = () => (int)gcPause.Mean;
        }
        else
        {
            timeInGc = (Func<int>?)typeof(GC).GetMethod("GetLastGCPercentTimeInGC",
                BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate(typeof(Func<int>));

            if (timeInGc == null) return;
        }

        meter.CreateObservableGauge($"{options.MetricPrefix}gc.pause.ratio", timeInGc, "%", "% Time in GC since last GC");
    }

    private static void JitInstrumentation(Meter meter, RuntimeMetricsOptions options)
    {
        meter.CreateObservableCounter($"{options.MetricPrefix}jit.il.bytes.total", () => System.Runtime.JitInfo.GetCompiledILBytes(), "B", description: "IL Bytes Jitted");

        meter.CreateObservableCounter($"{options.MetricPrefix}git.method.total", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Number of Methods Jitted");

        meter.CreateObservableCounter($"{options.MetricPrefix}jit.time.total", () => System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, "ms", description: "Time spent in JIT");
    }
#endif
    private static void ProcessInstrumentation(Meter meter)
    {
        meter.CreateObservableCounter("process.cpu.time", GetProcessorTimes, "s", "Processor time of this process");

        // Not yet official: https://github.com/open-telemetry/opentelemetry-specification/pull/2392
        meter.CreateObservableUpDownCounter("process.cpu.count", () => Environment.ProcessorCount, description: "The number of available logical CPUs");
        meter.CreateObservableUpDownCounter("process.memory.usage", () => Process.GetCurrentProcess().WorkingSet64, "B", "The amount of physical memory in use");
        meter.CreateObservableUpDownCounter("process.memory.virtual", () => Process.GetCurrentProcess().VirtualMemorySize64, "B", "The amount of committed virtual memory");

        meter.CreateObservableGauge("process.cpu.usage",
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? CpuUtilization.GetCpuUsage
                : new Func<double>(ProcessTimes.GetCpuUsage),
            "%", "CPU usage");

        meter.CreateObservableUpDownCounter("process.handle.count", () => Process.GetCurrentProcess().HandleCount, description: "Process handle count");
        meter.CreateObservableUpDownCounter("process.thread.count", () => Process.GetCurrentProcess().Threads.Count, description: "Process thread count");
    }

    private static IEnumerable<Measurement<double>> GetProcessorTimes()
    {
        var process = Process.GetCurrentProcess();

        return new[]
        {
            CreateMeasurement(process.UserProcessorTime.TotalSeconds, LabelState, "user"),
            CreateMeasurement(process.PrivilegedProcessorTime.TotalSeconds, LabelState, "system")
        };
    }
#if NET
    private static void SocketsInstrumentation(Meter meter, RuntimeMetricsOptions options,
        SocketsEventParser.Events.CountersV5_0? socketCounters)
    {
        if (socketCounters == null) return;

        var lastEstablishedOutgoing = -1d;
        socketCounters.OutgoingConnectionsEstablished += e => lastEstablishedOutgoing = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.connections.established.outgoing.total",
            () => lastEstablishedOutgoing < 0
                ? Array.Empty<Measurement<double>>()
                : new[] { new Measurement<double>(lastEstablishedOutgoing) },
            description: "The total number of outgoing established TCP connections");

        var lastEstablishedIncoming = -1d;
        socketCounters.IncomingConnectionsEstablished += e => lastEstablishedIncoming = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.connections.established.incoming.total",
            () => lastEstablishedIncoming < 0
                ? Array.Empty<Measurement<double>>()
                : new[] { new Measurement<double>(lastEstablishedIncoming) },
            description: "The total number of incoming established TCP connections");

        var lastReceived = -1d;
        socketCounters.BytesReceived += e => lastReceived = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.bytes.received.total", () => lastReceived < 0
            ? Array.Empty<Measurement<double>>()
            : new[] { new Measurement<double>(lastReceived) }, "B", "The total number of bytes received over the network");

        var lastSent = -1d;
        socketCounters.BytesSent += e => lastSent = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.bytes.sent.total", () => lastSent < 0
            ? Array.Empty<Measurement<double>>()
            : new[] { new Measurement<double>(lastSent) }, "B", "The total number of bytes sent over the network");
    }
#endif
    private static void ThreadingInstrumentation(Meter meter, RuntimeMetricsOptions options,
#if NETFRAMEWORK
        FrameworkEventParser.Events.Verbose? frameworkVerbose,
#endif
        NativeEvent.INativeEvent? nativeEvent)
    {
#if NET
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.thread.count", () => ThreadPool.ThreadCount, description: "ThreadPool thread count");
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool queue length");

        meter.CreateObservableCounter($"{options.MetricPrefix}threadpool.completed.items.total", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool completed work item count");
        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.timer.count", () => Timer.ActiveCount, description: "Number of active timers");
#else
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

            meter.CreateObservableCounter($"{options.MetricPrefix}threadpool.completed.items.total", () => completedItems, description: "ThreadPool completed work item count");
            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.queue.length", () => Math.Max(0, total), description: "ThreadPool queue length");
        }
#endif
#if !NET7_0_OR_GREATER
        if (nativeEvent != null)
        {
            var adjustmentsTotal = meter.CreateCounter<int>(
                $"{options.MetricPrefix}threadpool.adjustments.total",
                description: "The total number of changes made to the size of the thread pool, labeled by the reason for change");
#if NETFRAMEWORK
            var threadCount = -1;

            nativeEvent.ThreadPoolAdjusted += e =>
            {
                threadCount = (int)e.NumThreads;

                adjustmentsTotal.Add(1, CreateTag(LabelAdjustmentReason, AdjustmentReasonToLabel[e.AdjustmentReason]));
            };

            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.thread.count",
                () => threadCount < 0 ? Array.Empty<Measurement<int>>() : new[] { new Measurement<int>(threadCount) },
                description: "ThreadPool thread count");
#else
            nativeEvent.ThreadPoolAdjusted += e =>
                adjustmentsTotal.Add(1, CreateTag(LabelAdjustmentReason, AdjustmentReasonToLabel[e.AdjustmentReason]));
#endif
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // IO threadpool only exists on windows

                var iocThreads = -1;
                nativeEvent.IoThreadPoolAdjusted += e => iocThreads = (int)e.NumThreads;

                meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.io.thread.count",
                    () => iocThreads < 0 ? Array.Empty<Measurement<int>>() : new[] { new Measurement<int>(iocThreads) },
                    description: "The number of active threads in the IO thread pool");
            }
        }
#endif
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            static int IoThreadCount()
            {
                ThreadPool.GetAvailableThreads(out _, out var t2);
                ThreadPool.GetMaxThreads(out _, out var t4);

                return t4 - t2;
            }

            meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.active.io.thread.count", IoThreadCount,
                description: "The number of active IO threads");
        }

        static int ThreadCount()
        {
            ThreadPool.GetAvailableThreads(out var t1, out var t2);
            ThreadPool.GetMaxThreads(out var t3, out var t4);

            return t3 - t1 + t4 - t2;
        }

        meter.CreateObservableUpDownCounter($"{options.MetricPrefix}threadpool.active.worker.thread.count", ThreadCount, description: "The number of active worker threads");
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }

    private class DisposableAction : IDisposable
    {
        private Action? _action;

        public DisposableAction(Action action) => _action = action;

        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
