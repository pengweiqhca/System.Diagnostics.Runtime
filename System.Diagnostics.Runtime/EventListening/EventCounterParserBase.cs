using System.Diagnostics.Tracing;
using System.Reflection;

namespace System.Diagnostics.Runtime.EventListening;

/// <summary>
/// A reflection-based event parser that can extract typed counter values for a given event counter source.
/// </summary>
/// <remarks>
/// While using reflection isn't ideal from a performance standpoint, this is fine for now- event counters are collected at most
/// every second so won't have to deal with high throughput of events.
/// </remarks>
/// <typeparam name="T"></typeparam>
public abstract class EventCounterParserBase<T> : IEventCounterParser<T>
    where T : ICounterEvents
{
    private readonly Dictionary<string, Action<IDictionary<string, object>>> _countersToParsers;
    private long _timeSinceLastCounter;

    protected EventCounterParserBase()
    {
        var eventNames = new HashSet<string>(GetType().GetInterfaces()
            .Where(x => typeof(ICounterEvents).IsAssignableFrom(x))
            .SelectMany(x => x.GetEvents(), (_, e) => e.Name));

        var eventsAndNameAttrs = GetType()
            .GetEvents()
            .Where(e => eventNames.Contains(e.Name))
            .Select(x => (@event: x, nameAttr: x.GetCustomAttribute<CounterNameAttribute>()))
            .ToArray();

        if (eventsAndNameAttrs.Length == 0)
            throw new Exception("Could not locate any events to map to event counters!");

        var eventsWithoutAttrs = eventsAndNameAttrs.Where(x => x.nameAttr == null).ToArray();
        if (eventsWithoutAttrs.Length > 0)
            throw new Exception($"All events part of an {nameof(ICounterEvents)} interface require a [{nameof(CounterNameAttribute)}] attribute. Events without attribute: {string.Join(", ", eventsWithoutAttrs.Select(x => x.@event.Name))}.");

        _countersToParsers = eventsAndNameAttrs.ToDictionary(
            k => k.nameAttr!.Name,
            v => GetParseFunction(v.@event, v.nameAttr!.Name)
        );
    }

    public abstract string EventSourceName { get; }
    public virtual EventKeywords Keywords => EventKeywords.All;
    public virtual int RefreshIntervalSeconds { get; set; } = 1;
#if NETFRAMEWORK
#endif
    public void ProcessEvent(EventWrittenEventArgs e)
    {
        if (e.EventName is not "EventCounters" ||
            e.Payload?[0] is not IDictionary<string, object> eventPayload) return;

        Interlocked.Exchange(ref _timeSinceLastCounter, Stopwatch.GetTimestamp());

        if (!eventPayload.TryGetValue("Name", out var p) || p is not string counterName ||
            !_countersToParsers.TryGetValue(counterName, out var parser)) return;

        parser(eventPayload);
    }

    private Action<IDictionary<string, object>> GetParseFunction(EventInfo @event, string counterName)
    {
        var eventField = GetType().GetField(@event.Name, BindingFlags.NonPublic | BindingFlags.Instance);

        if (eventField == null || @event.EventHandlerType == null)
            throw new Exception($"Unable to locate backing field for event '{@event.Name}'.");

        var type = @event.EventHandlerType.GetGenericArguments().Single();

        Func<IDictionary<string, object>, (bool, object)> parseCounterFunc =
            type == typeof(IncrementingCounterValue)
                ? TryParseIncrementingCounter
                : type == typeof(MeanCounterValue)
                    ? TryParseCounter
                    : throw new Exception($"Unexpected counter type '{type}'!");

        return payload =>
        {
            var eventDelegate = (MulticastDelegate?)eventField.GetValue(this);

            // No-one is listening to this event
            if (eventDelegate == null) return;

            foreach (var handler in eventDelegate.GetInvocationList())
            {
                var (success, value) = parseCounterFunc(payload);
                if (success)
                    handler.Method.Invoke(handler.Target, new[] { value });
                else
                {
                    throw new MismatchedCounterTypeException($"Counter '{counterName}' could not be parsed by function {parseCounterFunc.Method} indicating the counter has been declared as the wrong type.");
                }
            }
        };
    }

    private static (bool, object) TryParseIncrementingCounter(IDictionary<string, object> payload) =>
        payload.TryGetValue("Increment", out var increment)
            ? (true, new IncrementingCounterValue((double)increment))
            : (false, new IncrementingCounterValue());

    private static (bool, object) TryParseCounter(IDictionary<string, object> payload) =>
        payload.TryGetValue("Mean", out var mean) && payload.TryGetValue("Count", out var count)
            ? (true, new MeanCounterValue((int)count, (double)mean))
            : (false, new MeanCounterValue());

    public void Dispose() { }
}

public class MismatchedCounterTypeException : Exception
{
    public MismatchedCounterTypeException(string message) : base(message)
    {
    }
}
