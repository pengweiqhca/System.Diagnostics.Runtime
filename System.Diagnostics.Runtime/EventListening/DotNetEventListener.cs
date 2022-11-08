using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening;

internal sealed class DotNetEventListener : EventListener
{
    internal DotNetEventListener(IEventListener eventListener, EventLevel level)
    {
        _eventListener = eventListener;
        _level = level;

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
            args["EventCounterIntervalSec"] = counterListener.RefreshIntervalSeconds.ToString();

        return args;
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // Event counters are present in every EventListener, regardless of if they subscribed to them.
        // Kind of odd but just filter them out by source here.
        if (eventData.EventSource.Name == _eventListener.EventSourceName)
            _eventListener.ProcessEvent(eventData);
    }

    public override void Dispose()
    {
        if (_eventListener is IDisposable disposable) disposable.Dispose();

        try
        {
            base.Dispose();
        }
        catch (AggregateException ex) when (ex.InnerException is ArgumentNullException) { }
    }
}
