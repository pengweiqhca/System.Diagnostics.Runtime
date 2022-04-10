using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ExceptionEventParser : IEventParser<ExceptionEventParser>, ExceptionEventParser.Events.Error
{
    public event Action<Events.ExceptionThrownEvent>? ExceptionThrown;

    public string EventSourceName => NativeRuntimeEventSource.Name;
    public EventKeywords Keywords => (EventKeywords)NativeRuntimeEventSource.Keywords.Exception;

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (e.EventId == NativeRuntimeEventSource.EventId.ExceptionThrown)
            ExceptionThrown?.Invoke(new ((string?)e.Payload?[0]));
    }

    public static class Events
    {
        public interface Error : IErrorEvents
        {
            event Action<ExceptionThrownEvent> ExceptionThrown;
        }

        public record struct ExceptionThrownEvent(string? ExceptionType);
    }
}
