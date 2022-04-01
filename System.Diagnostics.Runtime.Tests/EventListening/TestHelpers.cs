using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using Fasterflect;

namespace System.Diagnostics.Runtime.Tests.EventListening;

public class TestHelpers
{
    public static EventWrittenEventArgs CreateEventWrittenEventArgs(int eventId, DateTime? timestamp = null, params object[] payload)
    {
        EventWrittenEventArgs args;
        var bindFlags = Flags.NonPublic | Flags.Instance;

        // In .NET 6.0, they changed the signature of these constructors- handle this annoyance
        if (typeof(EventWrittenEventArgs).GetConstructors(bindFlags).Any(x => x.GetParameters().Length == 1))
        {
            args = (EventWrittenEventArgs)typeof(EventWrittenEventArgs).CreateInstance(new[] { typeof(EventSource) }, Flags.NonPublic | Flags.Instance, new object?[] { null });

            args.SetPropertyValue(nameof(args.EventId), eventId);
        }
        else
            args = (EventWrittenEventArgs)typeof(EventWrittenEventArgs).CreateInstance(new[] { typeof(EventSource), typeof(int) }, Flags.NonPublic | Flags.Instance, null, eventId);

        args.SetPropertyValue(nameof(args.Payload), new ReadOnlyCollection<object>(payload));
#if NETCOREAPP
        if (timestamp.HasValue)
            args.SetPropertyValue(nameof(args.TimeStamp), timestamp.Value);
#endif
        return args;
    }

    public static EventWrittenEventArgs CreateCounterEventWrittenEventArgs(params (string key, object val)[] payload)
    {
        var counterPayload = payload.ToDictionary(k => k.key, v => v.val);

        var e = CreateEventWrittenEventArgs(-1, DateTime.UtcNow, counterPayload);

        e.SetPropertyValue(nameof(e.EventName), "EventCounters");

        return e;
    }

    public static EventAssertion<T> ArrangeEventAssertion<T>(Action<Action<T>> wireUp)
    {
        return new EventAssertion<T>(wireUp);
    }

    public class EventAssertion<T>
    {
        public EventAssertion(Action<Action<T>> wireUp)
        {
            wireUp(e => History.Add(e));
        }

        public bool Fired => History.Count > 0;
        public List<T> History { get; } = new List<T>();
        public T LastEvent => History.Last();

    }
}
