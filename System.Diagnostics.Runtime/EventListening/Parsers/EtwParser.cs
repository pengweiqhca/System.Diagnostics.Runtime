﻿#if NETFRAMEWORK
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

//https://github.com/microsoft/perfview/blob/main/documentation/TraceEvent/TraceEventProgrammersGuide.md
//https://labs.criteo.com/2018/07/grab-etw-session-providers-and-events/
public class EtwParser : IDisposable, NativeEvent.INativeEvent
{
    // flags representing the "Garbage Collection" + "Preparation for garbage collection" pause reasons
    private const GCSuspendEEReason SuspendGcReasons = GCSuspendEEReason.SuspendForGC | GCSuspendEEReason.SuspendForGCPrep;
    private static readonly int ProcessId = Process.GetCurrentProcess().Id;

    private readonly TraceEventSession _session;
    private readonly EventTimer<int> _contentionTimer = new();
    private readonly EventTimer<int> _gcPauseTimer = new();
    private readonly EventTimer<int> _gcTimer = new();

    public EtwParser(string etwSessionName)
    {
        try
        {
            _session = new(etwSessionName, TraceEventSessionOptions.Attach);

            _session.Dispose(); // Try delete the exits session.
        }
        catch
        {
            // ignored
        }

        _session = new(etwSessionName,
            TraceEventSessionOptions.Create |
            TraceEventSessionOptions.NoRestartOnCreate |
            TraceEventSessionOptions.NoPerProcessorBuffering);
        try
        {
            _session.Source.Clr.ContentionStart += ContentionStart;
        }
        catch (Exception)
        {
            _session.Dispose(); // Try delete the exits session.

            _session = new(etwSessionName,
                TraceEventSessionOptions.Create |
                TraceEventSessionOptions.NoRestartOnCreate |
                TraceEventSessionOptions.NoPerProcessorBuffering);
            try
            {
                _session.Source.Clr.ContentionStart += ContentionStart;
            }
            catch (Exception)
            {
                _session.Dispose();

                throw;
            }
        }

        _session.Source.Clr.ContentionStop += ContentionStop;
        _session.Source.Clr.GCHeapStats += GCHeapStats;
        _session.Source.Clr.GCSuspendEEStart += GCSuspendEEStart;
        _session.Source.Clr.GCRestartEEStop += GCRestartEEStop;
        _session.Source.Clr.GCStart += GCStart;
        _session.Source.Clr.GCStop += GCStop;
        _session.Source.Clr.GCAllocationTick += GCAllocationTick;
        _session.Source.Clr.GCPerHeapHistory += GCPerHeapHistory;
        _session.Source.Clr.ThreadPoolWorkerThreadAdjustmentAdjustment += ThreadPoolWorkerThreadAdjustment;
        _session.Source.Clr.IOThreadCreationStart += IOThreadAdjustment;
        _session.Source.Clr.IOThreadRetirementStop += IOThreadAdjustment;
        _session.Source.Clr.ThreadPoolWorkerThreadStart += WorkerThreadAdjustment;
        _session.Source.Clr.ThreadPoolWorkerThreadRetirementStop += WorkerThreadAdjustment;
        _session.Source.Clr.ThreadPoolWorkerThreadWait += WorkerThreadAdjustment;

        try
        {
            _session.EnableProvider(
                ClrTraceEventParser.ProviderGuid,
                TraceEventLevel.Verbose,
                (ulong)(
                    ClrTraceEventParser.Keywords.Contention | // thread contention timing
                    ClrTraceEventParser.Keywords.GC | // garbage collector details
                    ClrTraceEventParser.Keywords.Threading // threadpool events
                ), new()
                {
                    //EnableInContainers = true,
                    //EnableSourceContainerTracking = true,
                    ProcessIDFilter = new List<int> { ProcessId },
                    EventIDsToEnable = new List<int>
                    {
                        NativeRuntimeEventSource.EventId.GcStart,
                        NativeRuntimeEventSource.EventId.GcEnd,
                        NativeRuntimeEventSource.EventId.RestartEEEnd,
                        NativeRuntimeEventSource.EventId.HeapStats,
                        NativeRuntimeEventSource.EventId.SuspendEE,
                        NativeRuntimeEventSource.EventId.AllocTick,
                        NativeRuntimeEventSource.EventId.IoThreadCreate,
                        NativeRuntimeEventSource.EventId.IoThreadTerminate,
                        NativeRuntimeEventSource.EventId.IoThreadRetire,
                        NativeRuntimeEventSource.EventId.IoThreadUnretire,
                        NativeRuntimeEventSource.EventId.WorkerThreadStart,
                        NativeRuntimeEventSource.EventId.WorkerThreadStop,
                        NativeRuntimeEventSource.EventId.WorkerThreadRetirementStart,
                        NativeRuntimeEventSource.EventId.WorkerThreadRetirementStop,
                        NativeRuntimeEventSource.EventId.ThreadPoolAdjustment,
                        NativeRuntimeEventSource.EventId.WorkerThreadWait,
                        NativeRuntimeEventSource.EventId.ContentionStart,
                        NativeRuntimeEventSource.EventId.ContentionStop,
                        NativeRuntimeEventSource.EventId.PerHeapHistory,
                    }
                });
        }
        catch
        {
            _session.Source.Clr.ContentionStart -= ContentionStart;
            _session.Source.Clr.ContentionStop -= ContentionStop;
            _session.Source.Clr.GCHeapStats -= GCHeapStats;
            _session.Source.Clr.GCSuspendEEStart -= GCSuspendEEStart;
            _session.Source.Clr.GCRestartEEStop -= GCRestartEEStop;
            _session.Source.Clr.GCStart -= GCStart;
            _session.Source.Clr.GCStop -= GCStop;
            _session.Source.Clr.GCAllocationTick -= GCAllocationTick;
            _session.Source.Clr.GCPerHeapHistory -= GCPerHeapHistory;
            _session.Source.Clr.ThreadPoolWorkerThreadAdjustmentAdjustment -= ThreadPoolWorkerThreadAdjustment;
            _session.Source.Clr.IOThreadCreationStart -= IOThreadAdjustment;
            _session.Source.Clr.IOThreadRetirementStop -= IOThreadAdjustment;
            _session.Source.Clr.ThreadPoolWorkerThreadStart -= WorkerThreadAdjustment;
            _session.Source.Clr.ThreadPoolWorkerThreadRetirementStop -= WorkerThreadAdjustment;
            _session.Source.Clr.ThreadPoolWorkerThreadWait -= WorkerThreadAdjustment;

            throw;
        }

        Task.Factory.StartNew(() =>
        {
            _session.Source.Process();

            _session.Source.Clr.ContentionStart -= ContentionStart;
            _session.Source.Clr.ContentionStop -= ContentionStop;
            _session.Source.Clr.GCHeapStats -= GCHeapStats;
            _session.Source.Clr.GCSuspendEEStart -= GCSuspendEEStart;
            _session.Source.Clr.GCRestartEEStop -= GCRestartEEStop;
            _session.Source.Clr.GCStart -= GCStart;
            _session.Source.Clr.GCStop -= GCStop;
            _session.Source.Clr.GCAllocationTick -= GCAllocationTick;
            _session.Source.Clr.GCPerHeapHistory -= GCPerHeapHistory;
            _session.Source.Clr.ThreadPoolWorkerThreadAdjustmentAdjustment -= ThreadPoolWorkerThreadAdjustment;
            _session.Source.Clr.IOThreadCreationStart -= IOThreadAdjustment;
            _session.Source.Clr.IOThreadRetirementStop -= IOThreadAdjustment;
            _session.Source.Clr.ThreadPoolWorkerThreadStart -= WorkerThreadAdjustment;
            _session.Source.Clr.ThreadPoolWorkerThreadRetirementStop -= WorkerThreadAdjustment;
            _session.Source.Clr.ThreadPoolWorkerThreadWait -= WorkerThreadAdjustment;
        }, TaskCreationOptions.LongRunning);
    }

    public event Action<NativeEvent.ContentionEndEvent>? ContentionEnd;
    public event Action<NativeEvent.HeapStatsEvent>? HeapStats;
    public event Action<NativeEvent.PauseCompleteEvent>? PauseComplete;
    public event Action<NativeEvent.CollectionStartEvent>? CollectionStart;
    public event Action<NativeEvent.CollectionCompleteEvent>? CollectionComplete;
    public event Action<NativeEvent.AllocationTickEvent>? AllocationTick;
    public event Action<NativeEvent.HeapFragmentationEvent>? HeapFragmentation;
    public event Action<NativeEvent.ThreadPoolAdjustedReasonEvent>? ThreadPoolAdjusted;
    public event Action<NativeEvent.ThreadPoolAdjustedEvent>? IoThreadPoolAdjusted;

    public event Action<NativeEvent.ThreadPoolAdjustedEvent>? WorkerThreadPoolAdjusted;

    private void ContentionStart(ContentionStartTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        _contentionTimer.Start(data.ThreadID, data.TimeStamp);
    }

    private void ContentionStop(ContentionStopTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (_contentionTimer.TryStop(data.ThreadID, data.TimeStamp, out var duration) &&
            duration > TimeSpan.Zero &&
            ContentionEnd is { } func)
            func(new(duration));
    }

    private void GCHeapStats(GCHeapStatsTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (HeapStats is { } func)
            func(new(data));
    }

    private void GCSuspendEEStart(GCSuspendEETraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if ((data.Reason & SuspendGcReasons) != 0)
            _gcPauseTimer.Start(1, data.TimeStamp);
    }

    private void GCRestartEEStop(GCNoUserDataTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (_gcPauseTimer.TryStop(1, data.TimeStamp, out var duration) &&
            duration > TimeSpan.Zero &&
            PauseComplete is { } func)
            func(new(duration));
    }

    private void GCStart(GCStartTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        _gcTimer.Start(data.Count, data.TimeStamp);

        if (CollectionStart is { } func)
            func(new((uint)data.Depth, (NativeRuntimeEventSource.GCReason)data.Reason,
                (NativeRuntimeEventSource.GCType)data.Type));
    }

    private void GCStop(GCEndTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (_gcTimer.TryStop(data.Count, data.TimeStamp, out var duration) &&
            duration > TimeSpan.Zero &&
            CollectionComplete is { } func)
            func(new((uint)data.Depth, duration));
    }

    private void GCAllocationTick(GCAllocationTickTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (AllocationTick is { } func)
            func(new((uint)data.AllocationAmount, data.AllocationKind == GCAllocationKind.Large));
    }

    private readonly long[] _fragmentedBytes = new long[(int)Gens.GenPinObj];

    private void GCPerHeapHistory(GCPerHeapHistoryTraceData data)
    {
        if (data.ProcessID != ProcessId || !data.VersionRecognized) return;

        for (var genNumber = Gens.Gen0; genNumber < Gens.GenPinObj; genNumber++)
            _fragmentedBytes[(int)genNumber] = data.GenData(genNumber).Fragmentation;

        if (HeapFragmentation is { } func) func(new(_fragmentedBytes));
    }

    private void ThreadPoolWorkerThreadAdjustment(ThreadPoolWorkerThreadAdjustmentTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (ThreadPoolAdjusted is { } func)
            func(new((uint)data.NewWorkerThreadCount, (NativeRuntimeEventSource.ThreadAdjustmentReason)data.Reason));
    }

    private void IOThreadAdjustment(IOThreadTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (IoThreadPoolAdjusted is { } func)
            func(new((uint)data.IOThreadCount, (uint)data.RetiredIOThreads));
    }

    private void WorkerThreadAdjustment(ThreadPoolWorkerThreadTraceData data)
    {
        if (data.ProcessID != ProcessId) return;

        if (WorkerThreadPoolAdjusted is { } func)
            func(new((uint)data.ActiveWorkerThreadCount, (uint)data.RetiredWorkerThreadCount));
    }

    // If not close, your can use this command close session: logman -ets stop <session name>
    public void Dispose() => _session.Dispose();
}
#endif
