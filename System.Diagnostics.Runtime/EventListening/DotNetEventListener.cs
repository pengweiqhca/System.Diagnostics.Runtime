using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening;

internal sealed class DotNetEventListener : EventListener
{
    private readonly GlobalOptions _globalOptions;
    private readonly string? _nameSnakeCase;
    private readonly Stopwatch? _sp;
    private readonly HashSet<long>? _threadIdsPublishingEvents;

    internal DotNetEventListener(IEventListener eventListener, EventLevel level, GlobalOptions globalOptions)
    {
        _eventListener = eventListener;
        _level = level;
        _globalOptions = globalOptions;

        if (_globalOptions.EnabledDebuggingMetrics)
        {
            _nameSnakeCase = eventListener.GetType().Name.ToSnakeCase();
            _sp = new Stopwatch();
            _threadIdsPublishingEvents = new HashSet<long>();

            _globalOptions.DebuggingMetrics.GetMeasurements = () => new[] { new Measurement<long>(_threadIdsPublishingEvents.Count, new KeyValuePair<string, object?>("listener_name", _nameSnakeCase)) };
        }

        EventSourceCreated += OnEventSourceCreated;
    }

    private readonly EventLevel _level;
    private readonly IEventListener _eventListener;

    internal bool StartedReceivingEvents { get; private set; }

    private void OnEventSourceCreated(object? _, EventSourceCreatedEventArgs e)
    {
        if (e.EventSource == null || e.EventSource.Name != _eventListener.EventSourceName) return;

        EnableEvents(e.EventSource, _level, _eventListener.Keywords, GetEventListenerArguments(_eventListener));

        StartedReceivingEvents = true;
    }

    private static Dictionary<string, string?> GetEventListenerArguments(IEventListener listener)
    {
        var args = new Dictionary<string, string?>();

        if (listener is IEventCounterListener counterListener)
        {
            args["EventCounterIntervalSec"] = counterListener.RefreshIntervalSeconds.ToString();
        }

        return args;
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (_globalOptions.EnabledDebuggingMetrics)
        {
            _globalOptions.DebuggingMetrics.EventTypeCounts.Add(1,
                    new KeyValuePair<string, object?>("listener_name", _nameSnakeCase),
                    new KeyValuePair<string, object?>("event_source_name", eventData.EventSource.Name),
                    new KeyValuePair<string, object?>("event_name", eventData.EventName ?? "unknown"));

            _sp!.Restart();
#if NETCOREAPP
            _threadIdsPublishingEvents!.Add(eventData.OSThreadId);
#else
            _threadIdsPublishingEvents!.Add(Environment.CurrentManagedThreadId);
#endif
        }

        // Event counters are present in every EventListener, regardless of if they subscribed to them.
        // Kind of odd but just filter them out by source here.
        if (eventData.EventSource.Name == _eventListener.EventSourceName)
            _eventListener.ProcessEvent(eventData);

        if (_globalOptions.EnabledDebuggingMetrics)
        {
            _sp!.Stop();

            _globalOptions.DebuggingMetrics.TimeConsumed.Add(_sp.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("listener_name", _nameSnakeCase),
                new KeyValuePair<string, object?>("event_source_name", eventData.EventSource.Name),
                new KeyValuePair<string, object?>("event_name", eventData.EventName ?? "unknown"));
        }
    }

    public override void Dispose()
    {
        if (_eventListener is IDisposable disposable) disposable.Dispose();

        base.Dispose();
    }

    internal class GlobalOptions
    {
        internal static GlobalOptions CreateFrom(Meter meter, RuntimeMetricsOptions options)
        {
            var instance = new GlobalOptions();

            if (options.EnabledDebuggingMetrics)
            {
                instance.DebuggingMetrics = new DebugMetrics(
                    meter.CreateCounter<long>($"{options.MetricPrefix}debug.event.count", null, "The total number of .NET diagnostic events processed"),
                    meter.CreateCounter<double>($"{options.MetricPrefix}debug.event.seconds.total", "ms",
                        "The total time consumed by processing .NET diagnostic events (does not include the CPU cost to generate the events)"),
                    meter.CreateObservableGauge($"{options.MetricPrefix}debug.publish.thread.count",
                      () => instance.DebuggingMetrics is { GetMeasurements: { } func } ? func() : Array.Empty<Measurement<long>>(),
                        null, "The number of threads that have published events")
                );
            }

            instance.EnabledDebuggingMetrics = options.EnabledDebuggingMetrics;

            return instance;
        }

        [MemberNotNullWhen(true, nameof(DebuggingMetrics))]
        public bool EnabledDebuggingMetrics { get; set; }
        public DebugMetrics? DebuggingMetrics { get; set; }

        public class DebugMetrics
        {
            public DebugMetrics(Counter<long> eventTypeCounts, Counter<double> timeConsumed, ObservableGauge<long> threadCount)
            {
                EventTypeCounts = eventTypeCounts;
                TimeConsumed = timeConsumed;
                ThreadCount = threadCount;
            }

            public Counter<double> TimeConsumed { get; }

            public Counter<long> EventTypeCounts { get; }

            public Func<IEnumerable<Measurement<long>>>? GetMeasurements { get; set; }

            public ObservableGauge<long> ThreadCount { get; }
        }
    }
}
