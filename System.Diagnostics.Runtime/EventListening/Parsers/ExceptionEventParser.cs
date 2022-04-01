using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ExceptionEventParser : IEventParser<ExceptionEventParser>, ExceptionEventParser.Events.Error
{
    private const int EventIdExceptionThrown = 80;

    public event Action<Events.ExceptionThrownEvent>? ExceptionThrown;

    public string EventSourceName => NativeRuntimeEventSource.Name;
    public EventKeywords Keywords => (EventKeywords)NativeRuntimeEventSource.Keywords.Exception;
#if NETFRAMEWORK
#endif
    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (e.EventId == EventIdExceptionThrown)
            ExceptionThrown?.Invoke(new Events.ExceptionThrownEvent((string?)e.Payload?[0]));
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
