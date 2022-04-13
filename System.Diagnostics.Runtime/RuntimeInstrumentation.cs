using System.Diagnostics.Metrics;
using System.Diagnostics.Runtime.EventListening;
using System.Diagnostics.Runtime.EventListening.Parsers;
using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Diagnostics.Runtime;

//https://github.com/dotnet/diagnostics/blob/main/src/Tools/dotnet-counters/KnownData.cs
public class RuntimeInstrumentation : IDisposable
{
    private const string
        LabelAdjustmentReason = "adjustment_reason",
        LabelType = "type",
        LabelReason = "gc_reason",
        LabelGcType = "gc_type",
        LabelHeap = "gc_heap",
        LabelGeneration = "gc_generation";

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
#if NETCOREAPP
        RuntimeEventParser? runtimeParser = null;
#else
        EtwParser? etlParser = null;

        void CreateEtlParser()
        {
            if (etlParser != null || string.IsNullOrWhiteSpace(options.EtwSessionName)) return;

            try
            {
                disposables!.Add(etlParser = new EtwParser(options.EtwSessionName!));
            }
            catch (Exception ex)
            {
                RuntimeEventSource.Log.EtlConstructException(ex);
            }
        }
#endif
        if (options.IsContentionEnabled)
        {
#if NETFRAMEWORK
            CreateEtlParser();

            ContentionInstrumentation(meter, options, new EventConsumer<ContentionEventParser.Events.Info>(etlParser));
#else
            ContentionEventParser? parser = null;
            if (options.EnabledNativeRuntime)
            {
                parser = new ContentionEventParser();

                disposables.Add(new DotNetEventListener(parser, EventLevel.Informational, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }

            ContentionInstrumentation(meter, options, new EventConsumer<ContentionEventParser.Events.Info>(parser));
#endif
        }
#if NET6_0_OR_GREATER
        if (options.IsDnsEnabled)
        {
            var parser = new NameResolutionEventParser();

            disposables.Add(new DotNetEventListener(parser, EventLevel.LogAlways, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));

            DnsInstrumentation(meter, options, new EventConsumer<NameResolutionEventParser.Events.CountersV5_0>(parser));
        }
#endif
        if (options.IsExceptionsEnabled)
        {
#if NETFRAMEWORK
            if (options.EnabledNativeRuntime)
            {
                CreateEtlParser();

                ExceptionsInstrumentation(meter, options,
                    new EventConsumer<ExceptionEventParser.Events.Error>(etlParser));
            }
#else
            ExceptionEventParser? parser = null;
            if (options.EnabledNativeRuntime)
            {
                parser = new ExceptionEventParser();

                disposables.Add(new DotNetEventListener(parser, EventLevel.Error, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }

            else if (options.EnabledSystemRuntime)
            {
                runtimeParser = new RuntimeEventParser();

                disposables.Add(new DotNetEventListener(runtimeParser, EventLevel.LogAlways, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }

            ExceptionsInstrumentation(meter, options,
                new EventConsumer<RuntimeEventParser.Events.CountersV3_0>(runtimeParser),
                new EventConsumer<ExceptionEventParser.Events.Error>(parser));
#endif
        }

        if (options.IsGcEnabled)
        {
#if NETCOREAPP
            GcEventParser? parser = null;

            if (options.EnabledNativeRuntime)
            {
                parser = new GcEventParser();

                disposables.Add(new DotNetEventListener(parser, EventLevel.Verbose, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }

            if (options.EnabledSystemRuntime && runtimeParser == null)
            {
                runtimeParser = new RuntimeEventParser();

                disposables.Add(new DotNetEventListener(runtimeParser, EventLevel.LogAlways, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }

            GcInstrumentation(meter, options,
#if NET6_0_OR_GREATER
                new EventConsumer<RuntimeEventParser.Events.CountersV5_0>(runtimeParser),
#endif
                new EventConsumer<RuntimeEventParser.Events.CountersV3_0>(runtimeParser),
                new EventConsumer<GcEventParser.Events.Verbose>(parser),
                new EventConsumer<GcEventParser.Events.Info>(parser));
#else
            if (options.EnabledNativeRuntime) CreateEtlParser();

            GcInstrumentation(meter, options,
                new EventConsumer<GcEventParser.Events.Verbose>(etlParser),
                new EventConsumer<GcEventParser.Events.Info>(etlParser));
#endif
        }
#if NET6_0_OR_GREATER
        if (options.IsJitEnabled) JitInstrumentation(meter, options);
#endif
        if (options.IsProcessEnabled) ProcessInstrumentation(meter);
#if NET6_0_OR_GREATER
        if (options.IsSocketsEnabled)
        {
            var parser = new SocketsEventParser();

            disposables.Add(new DotNetEventListener(parser, EventLevel.LogAlways, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));

            SocketsInstrumentation(meter, options, new EventConsumer<SocketsEventParser.Events.CountersV5_0>(parser));
        }
#endif
        if (options.IsThreadingEnabled)
        {
#if NETCOREAPP
            ThreadPoolEventParser? parser = null;
            if (options.EnabledNativeRuntime)
            {
                parser = new ThreadPoolEventParser();

                disposables.Add(new DotNetEventListener(parser, EventLevel.Informational, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }
#else
            ThreadPoolSchedulingParser? schedulingParser = null;
            if (options.EnabledNativeRuntime)
            {
                CreateEtlParser();

                schedulingParser = new ThreadPoolSchedulingParser();

                disposables.Add(new DotNetEventListener(schedulingParser, EventLevel.Verbose, DotNetEventListener.GlobalOptions.CreateFrom(meter, options)));
            }
#endif
            ThreadingInstrumentation(meter, options,
#if NETFRAMEWORK
                new EventConsumer<ThreadPoolSchedulingParser.Events.Verbose>(schedulingParser),
                new EventConsumer<ThreadPoolEventParser.Events.Info>(etlParser));
#else
                new EventConsumer<ThreadPoolEventParser.Events.Info>(parser));
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
        meter.CreateObservableGauge($"{options.MetricPrefix}assembly.count", () => AppDomain.CurrentDomain.GetAssemblies().Length, description: "Number of Assemblies Loaded");

    private static void ContentionInstrumentation(Meter meter, RuntimeMetricsOptions options,
        IConsumes<ContentionEventParser.Events.Info> contentionInfo)
    {
#if NETCOREAPP
        meter.CreateObservableCounter($"{options.MetricPrefix}lock.contention.total", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
#endif
        if (!contentionInfo.Enabled) return;

        var contentionSecondsTotal = meter.CreateCounter<double>($"{options.MetricPrefix}lock.contention.time.total", "s", "The total amount of time spent contending locks");
#if NETFRAMEWORK
        var contentionTotal = meter.CreateCounter<long>($"{options.MetricPrefix}lock.contention.total", description: "Monitor Lock Contention Count");
        contentionInfo.Events.ContentionEnd += e =>
        {
            contentionSecondsTotal.Add(e.ContentionDuration.TotalSeconds);

            contentionTotal.Add(1);
        };
#else
        contentionInfo.Events.ContentionEnd += e => contentionSecondsTotal.Add(e.ContentionDuration.TotalSeconds);
#endif
    }
#if NET6_0_OR_GREATER
    private static void DnsInstrumentation(Meter meter, RuntimeMetricsOptions options,
        IConsumes<NameResolutionEventParser.Events.CountersV5_0> nameResolutionCounters)
    {
        if (!nameResolutionCounters.Enabled) return;

        var dnsLookupsRequested = 0L;
        nameResolutionCounters.Events.DnsLookupsRequested += e => dnsLookupsRequested = (long)e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}dns.requested.total",
            () => dnsLookupsRequested,
            description: "The total number of dns lookup requests");

        var currentDnsLookups = 0L;
        nameResolutionCounters.Events.CurrentDnsLookups += e => currentDnsLookups = (long)e.Mean;
        meter.CreateObservableGauge($"{options.MetricPrefix}dns.current.count",
            () => currentDnsLookups,
            description: "The total number of current dns lookups");

        var dnsLookupsDuration = 0.0;
        nameResolutionCounters.Events.DnsLookupsDuration += e => dnsLookupsDuration = e.Total;
        meter.CreateObservableCounter($"{options.MetricPrefix}dns.duration.total", () => dnsLookupsDuration, "ms", "The sum of dns lookup durations");
    }
#endif
    private static void ExceptionsInstrumentation(Meter meter, RuntimeMetricsOptions options,
#if NETCOREAPP
        IConsumes<RuntimeEventParser.Events.CountersV3_0> runtimeCounters,
#endif
        IConsumes<ExceptionEventParser.Events.Error> exceptionError)
    {
        if (exceptionError.Enabled)
        {
            var exceptionCount = meter.CreateCounter<long>(
                $"{options.MetricPrefix}exception.total",
                description: "Count of exceptions thrown, broken down by type");

            exceptionError.Events.ExceptionThrown += e => exceptionCount.Add(1, CreateTag(LabelType, e.ExceptionType));
        }
#if NETCOREAPP
        else if (runtimeCounters.Enabled)
        {
            var exceptionCount = meter.CreateCounter<long>(
                $"{options.MetricPrefix}exception.total",
                description: "Count of exceptions thrown");

            runtimeCounters.Events.ExceptionCount += e => exceptionCount.Add((long)e.IncrementedBy);
        }
        else if (typeof(Exception).GetMethod("GetExceptionCount",
                     BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate(typeof(Func<uint>)) is Func<uint> func)
            meter.CreateObservableCounter<long>(
                $"{options.MetricPrefix}exception.total",
                () => func(),
                description: "Count of exceptions thrown");
#endif
    }

    private static void GcInstrumentation(Meter meter, RuntimeMetricsOptions options,
#if NETCOREAPP
#if NET6_0_OR_GREATER
        IConsumes<RuntimeEventParser.Events.CountersV5_0> runtime5Counters,
#endif
        IConsumes<RuntimeEventParser.Events.CountersV3_0> runtimeCounters,
#endif
        IConsumes<GcEventParser.Events.Verbose> gcVerbose,
        IConsumes<GcEventParser.Events.Info> gcInfo)
    {
        if (gcVerbose.Enabled)
        {
            var allocated = meter.CreateCounter<long>($"{options.MetricPrefix}gc.allocated.total", "B", "Allocation bytes since process start");

            gcVerbose.Events.AllocationTick += e => allocated.Add(e.AllocatedBytes, CreateTag(LabelHeap, e.IsLargeObjectHeap ? "loh" : "soh"));
        }
#if NETCOREAPP
        else
            meter.CreateObservableCounter($"{options.MetricPrefix}gc.allocated.total", () => GC.GetTotalAllocatedBytes(), "B", "Allocation bytes since process start");

        meter.CreateObservableGauge($"{options.MetricPrefix}gc.fragmentation", GetFragmentation, "%", "GC fragmentation");
        meter.CreateObservableGauge($"{options.MetricPrefix}gc.memory.total.available",
            () => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            "B", "The upper limit on the amount of physical memory .NET can allocate to");
#if NET6_0_OR_GREATER
        meter.CreateObservableCounter($"{options.MetricPrefix}gc.committed.total", () => GC.GetGCMemoryInfo().TotalCommittedBytes, "B", description: "GC Committed bytes since process start");
#endif
        TimeInGc(meter, options, runtimeCounters);
#endif
        if (gcInfo.Enabled)
        {
            var gcCollectionSeconds = meter.CreateHistogram<double>(
                $"{options.MetricPrefix}gc.collection.time", "ms",
                "The amount of time spent running garbage collections");

            gcInfo.Events.CollectionComplete += e => gcCollectionSeconds.Record(e.Duration.TotalMilliseconds,
                CreateTag(LabelGeneration, GetGenerationToString(e.Generation)),
                CreateTag(LabelGcType, GcTypeToLabels[e.Type]));

            var gcPauseSeconds = meter.CreateHistogram<double>(
                $"{options.MetricPrefix}gc.pause.time", "ms",
                "The amount of time execution was paused for garbage collection");

            gcInfo.Events.PauseComplete += e => gcPauseSeconds.Record(e.PauseDuration.TotalMilliseconds);

            var gcCollections = meter.CreateCounter<int>(
                $"{options.MetricPrefix}gc.collection.total",
                description: "Counts the number of garbage collections that have occurred, broken down by generation number and the reason for the collection.");

            gcInfo.Events.CollectionStart += e => gcCollections.Add(1,
                CreateTag(LabelGeneration, GetGenerationToString(e.Generation)),
                CreateTag(LabelReason, GcReasonToLabels[e.Reason]));

            GcEventParser.Events.HeapStatsEvent stats = default;
            gcInfo.Events.HeapStats += e => stats = e;

            meter.CreateObservableGauge($"{options.MetricPrefix}gc.heap.size", () => stats == default
                ? Array.Empty<Measurement<long>>()
                : new[]
                {
                    CreateMeasurement(stats.Gen0SizeBytes, LabelGeneration, "0"),
                    CreateMeasurement(stats.Gen1SizeBytes, LabelGeneration, "1"),
                    CreateMeasurement(stats.Gen2SizeBytes, LabelGeneration, "2"),
                    CreateMeasurement(stats.LohSizeBytes, LabelGeneration, "loh")
#if NET6_0_OR_GREATER
                    , CreateMeasurement(stats.PohSizeBytes, LabelGeneration, "poh")
#endif
                }, "B", "The current size of all heaps (only updated after a garbage collection)");

            meter.CreateObservableGauge($"{options.MetricPrefix}gc.pinned.objects",
                () => stats == default ? Array.Empty<Measurement<int>>() : new[] { new Measurement<int>(stats.NumPinnedObjects) },
                description: "The number of pinned objects");

            meter.CreateObservableGauge($"{options.MetricPrefix}gc.finalization.queue.length",
                () => stats == default ? Array.Empty<Measurement<long>>() : new[] { new Measurement<long>(stats.FinalizationQueueLength) },
                description: "The number of objects waiting to be finalized");
        }
        else
        {
            meter.CreateObservableCounter($"{options.MetricPrefix}gc.collection.total", () => new[]
            {
                CreateMeasurement(GC.CollectionCount(0), LabelGeneration, "0"),
                CreateMeasurement(GC.CollectionCount(1), LabelGeneration, "1"),
                CreateMeasurement(GC.CollectionCount(2), LabelGeneration, "2")
            }, description: "Counts the number of garbage collections that have occurred");
#if NETCOREAPP
            if (runtimeCounters.Enabled)
            {
                var heapSize = new HeapSize();

                meter.CreateObservableGauge($"{options.MetricPrefix}gc.heap.size",
                    () => heapSize.Gen0SizeBytes > 0
                        ? new[]
                        {
                            CreateMeasurement(heapSize.Gen0SizeBytes, LabelGeneration, "0"),
                            CreateMeasurement(heapSize.Gen1SizeBytes, LabelGeneration, "1"),
                            CreateMeasurement(heapSize.Gen2SizeBytes, LabelGeneration, "2"),
                            CreateMeasurement(heapSize.LohSizeBytes, LabelGeneration, "loh")
#if NET6_0_OR_GREATER
                            , CreateMeasurement(heapSize.PohSizeBytes, LabelGeneration, "poh")
#endif
                        }
                        : Array.Empty<Measurement<long>>(),
                    "B", "The current size of all heaps (only updated after a garbage collection)");

                runtimeCounters.Events.Gen0Size += e => heapSize.Gen0SizeBytes = (long)e.Mean;
                runtimeCounters.Events.Gen1Size += e => heapSize.Gen1SizeBytes = (long)e.Mean;
                runtimeCounters.Events.Gen2Size += e => heapSize.Gen2SizeBytes = (long)e.Mean;
                runtimeCounters.Events.LohSize += e => heapSize.LohSizeBytes = (long)e.Mean;
#if NET6_0_OR_GREATER
                if (runtime5Counters.Enabled)
                    runtime5Counters.Events.PohSize += e => heapSize.PohSizeBytes = (long)e.Mean;
#endif
            }
            else if (typeof(GC).GetMethod("GetGenerationSize", BindingFlags.Static | BindingFlags.NonPublic)?
                         .CreateDelegate(typeof(Func<int, ulong>)) is Func<int, ulong> func)
                meter.CreateObservableGauge($"{options.MetricPrefix}gc.heap.size",
                    () => new[]
                    {
                        CreateMeasurement((long)func(0), LabelGeneration, "0"),
                        CreateMeasurement((long)func(1), LabelGeneration, "1"),
                        CreateMeasurement((long)func(2), LabelGeneration, "2"),
                        CreateMeasurement((long)func(3), LabelGeneration, "loh")
#if NET6_0_OR_GREATER
                        , CreateMeasurement((long)func(4), LabelGeneration, "poh")
#endif
                    },
                    "B", "The current size of all heaps (only updated after a garbage collection)");
            else
#endif
            {
                meter.CreateObservableGauge($"{options.MetricPrefix}gc.heap.size", () => GC.GetTotalMemory(false), "B", "The current size of all heaps");
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
#if NETCOREAPP
    private static double GetFragmentation()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        return gcInfo.HeapSizeBytes == 0 ? 0 : gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes;
    }

    private class HeapSize
    {
        public long Gen0SizeBytes { get; set; }
        public long Gen1SizeBytes { get; set; }
        public long Gen2SizeBytes { get; set; }
        public long LohSizeBytes { get; set; }
#if NET6_0_OR_GREATER
        public long PohSizeBytes { get; set; }
#endif
    }

    private static void TimeInGc(Meter meter, RuntimeMetricsOptions options,
        IConsumes<RuntimeEventParser.Events.CountersV3_0> runtimeCounters)
    {
        Func<int> timeInGc;
        if (runtimeCounters.Enabled)
        {
            MeanCounterValue gcPause = default;

            runtimeCounters.Events.TimeInGc += e => gcPause = e;

            timeInGc = () => (int)gcPause.Mean;
        }
        else
        {
            timeInGc = (Func<int>)typeof(GC).GetMethod("GetLastGCPercentTimeInGC",
                BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate(typeof(Func<int>))!;

            if (timeInGc == null!) return;
        }

        meter.CreateObservableGauge($"{options.MetricPrefix}gc.pause.ratio", timeInGc, "%", "% Time in GC since last GC");
    }
#endif
#if NET6_0_OR_GREATER
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
        meter.CreateObservableGauge("process.cpu.count", () => Environment.ProcessorCount, description: "The number of available logical CPUs");
        meter.CreateObservableGauge("process.memory.usage", () => Environment.WorkingSet, "B", "The amount of physical memory in use");
        meter.CreateObservableGauge("process.memory.virtual", () => Process.GetCurrentProcess().VirtualMemorySize64, "B", "The amount of committed virtual memory");

        meter.CreateObservableGauge("process.cpu.usage",
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? CpuUtilization.GetCpuUsage
                : new Func<int>(ProcessTimes.GetCpuUsage),
            "%", "CPU usage");

        meter.CreateObservableGauge("process.handle.count", () => Process.GetCurrentProcess().HandleCount, description: "Process handle count");
        meter.CreateObservableGauge("process.thread.count", () => Process.GetCurrentProcess().Threads.Count, description: "Process thread count");
    }

    private static IEnumerable<Measurement<double>> GetProcessorTimes()
    {
        var process = Process.GetCurrentProcess();

        return new[]
        {
            CreateMeasurement(process.UserProcessorTime.TotalSeconds, "state", "user"),
            CreateMeasurement(process.PrivilegedProcessorTime.TotalSeconds, "state", "system")
        };
    }
#if NET6_0_OR_GREATER
    private static void SocketsInstrumentation(Meter meter, RuntimeMetricsOptions options,
        IConsumes<SocketsEventParser.Events.CountersV5_0> socketCounters)
    {
        if (!socketCounters.Enabled) return;

        var lastEstablishedOutgoing = 0.0;
        socketCounters.Events.OutgoingConnectionsEstablished += e => lastEstablishedOutgoing = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.connections.established.outgoing.total",
            () => lastEstablishedOutgoing,
            description: "The total number of outgoing established TCP connections");

        var lastEstablishedIncoming = 0.0;
        socketCounters.Events.IncomingConnectionsEstablished += e => lastEstablishedIncoming = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.connections.established.incoming.total",
            () => lastEstablishedIncoming,
            description: "The total number of incoming established TCP connections");

        var lastReceived = 0.0;
        socketCounters.Events.BytesReceived += e => lastReceived = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.bytes.received.total", () => lastReceived, "B", "The total number of bytes received over the network");

        var lastSent = 0.0;
        socketCounters.Events.BytesSent += e => lastSent = e.Mean;
        meter.CreateObservableCounter($"{options.MetricPrefix}sockets.bytes.sent.total", () => lastSent, "B", "The total number of bytes sent over the network");
    }
#endif
    private static void ThreadingInstrumentation(Meter meter, RuntimeMetricsOptions options,
#if NETFRAMEWORK
        IConsumes<ThreadPoolSchedulingParser.Events.Verbose> threadPoolSchedulingInfo,
#endif
        IConsumes<ThreadPoolEventParser.Events.Info> threadPoolInfo)
    {
#if NETCOREAPP
        meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.thread.count", () => ThreadPool.ThreadCount, description: "ThreadPool thread count");
        meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool queue length");

        meter.CreateObservableCounter($"{options.MetricPrefix}threadpool.completed.items.total", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool completed work item count");
        meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.timer.count", () => Timer.ActiveCount, description: "Number of active timers");
#else
        if (threadPoolSchedulingInfo.Enabled)
        {
            var completedItems = 0L;
            var total = 0L;

            threadPoolSchedulingInfo.Events.Enqueue += () => Interlocked.Increment(ref total);
            threadPoolSchedulingInfo.Events.Dequeue += () =>
            {
                Interlocked.Increment(ref completedItems);
                if (Interlocked.Read(ref total) > 0) Interlocked.Decrement(ref total);
            };

            meter.CreateObservableCounter($"{options.MetricPrefix}threadpool.completed.items.total", () => completedItems, description: "ThreadPool completed work item count");
            meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.queue.length", () => Math.Max(0, total), description: "ThreadPool queue length");
        }
#endif
        if (threadPoolInfo.Enabled)
        {
            var adjustmentsTotal = meter.CreateCounter<int>(
                $"{options.MetricPrefix}threadpool.adjustments.total",
                description: "The total number of changes made to the size of the thread pool, labeled by the reason for change");
#if NETFRAMEWORK
            var threadCount = 0;

            threadPoolInfo.Events.ThreadPoolAdjusted += e =>
            {
                threadCount = (int)e.NumThreads;

                adjustmentsTotal.Add(1, CreateTag(LabelAdjustmentReason, AdjustmentReasonToLabel[e.AdjustmentReason]));
            };

            meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.thread.count", () => threadCount, description: "ThreadPool thread count");
#else
            threadPoolInfo.Events.ThreadPoolAdjusted += e =>
                adjustmentsTotal.Add(1, CreateTag(LabelAdjustmentReason, AdjustmentReasonToLabel[e.AdjustmentReason]));
#endif

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // IO threadpool only exists on windows

                var iocThreads = 0;
                threadPoolInfo.Events.IoThreadPoolAdjusted += e => iocThreads = (int)e.NumThreads;

                meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.io.thread.count",
                    () => iocThreads,
                    description: "The number of active threads in the IO thread pool");
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            static int IoThreadCount()
            {
                ThreadPool.GetAvailableThreads(out _, out var t2);
                ThreadPool.GetMaxThreads(out _, out var t4);

                return t4 - t2;
            }

            meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.active.io.thread.count", IoThreadCount,
                description: "The number of active IO threads");
        }

        static int ThreadCount()
        {
            ThreadPool.GetAvailableThreads(out var t1, out var t2);
            ThreadPool.GetMaxThreads(out var t3, out var t4);

            return t3 - t1 + t4 - t2;
        }

        meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.active.worker.thread.count", ThreadCount, description: "The number of active worker threads");
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }
}
