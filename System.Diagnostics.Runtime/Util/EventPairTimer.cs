using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.Util;

/// <summary>
/// To generate metrics, we are often interested in the duration between two events. This class
/// helps time the duration between two events.
/// </summary>
/// <typeparam name="TId">A type of an identifier present on both events</typeparam>
/// <typeparam name="TEventData">A struct that represents data of interest extracted from the first event</typeparam>
public class EventPairTimer<TId, TEventData>(
    int startEventId,
    int endEventId,
    Func<EventWrittenEventArgs, TId> extractEventIdFn,
    Func<EventWrittenEventArgs, TEventData> extractData,
    Cache<TId, TEventData>? cache = null)
    where TId : struct
    where TEventData : struct
{
    private readonly Cache<TId, TEventData> _eventStartedAtCache = cache ?? new Cache<TId, TEventData>(TimeSpan.FromMinutes(1));

    /// <summary>
    /// Checks if an event is an expected final event- if so, returns true, the duration between it and the start event and
    /// any data extracted from the first event.
    /// </summary>
    /// <remarks>
    /// If the event id matches the supplied start event id, then we cache the event until the final event occurs.
    /// All other events are ignored.
    /// </remarks>
    public DurationResult TryGetDuration(EventWrittenEventArgs e, out TimeSpan duration, out TEventData startEventData)
    {
        duration = TimeSpan.Zero;
        startEventData = default;

        if (e.EventId == startEventId)
        {
#if NET
            _eventStartedAtCache.Set(extractEventIdFn(e), startEventData = extractData(e), e.TimeStamp);
#else
            _eventStartedAtCache.Set(extractEventIdFn(e), startEventData = extractData(e), DateTime.Now);
#endif
            return DurationResult.Start;
        }

        if (e.EventId != endEventId) return DurationResult.Unrecognized;

        if (!_eventStartedAtCache.TryRemove(extractEventIdFn(e), out startEventData, out var timeStamp))
            return DurationResult.FinalWithoutDuration;
#if NET
        duration = e.TimeStamp - timeStamp;
#else
        duration = DateTime.Now - timeStamp;
#endif
        return DurationResult.FinalWithDuration;
    }
}

public enum DurationResult
{
    Unrecognized = 0,
    Start = 1,
    FinalWithoutDuration = 2,
    FinalWithDuration = 3
}

public sealed class EventPairTimer<TId>(
    int startEventId,
    int endEventId,
    Func<EventWrittenEventArgs, TId> extractEventIdFn,
    Cache<TId, int>? cache = null)
    : EventPairTimer<TId, int>(startEventId, endEventId, extractEventIdFn, _ => 0, cache)
    where TId : struct
{
    public DurationResult TryGetDuration(EventWrittenEventArgs e, out TimeSpan duration) =>
        TryGetDuration(e, out duration, out _);
}
#if NETFRAMEWORK
public class EventTimer<TId, TData>(Cache<TId, TData>? cache = null)
    where TId : struct
    where TData : struct
{
    private readonly Cache<TId, TData> _eventStartedAtCache = cache ?? new(TimeSpan.FromMinutes(1));

    public void Start(TId key, TData data, DateTime timeStamp) =>
        _eventStartedAtCache.Set(key, data, timeStamp);

    public bool TryStop(TId key, DateTime timeStamp, out TimeSpan duration, out TData startEventData)
    {
        duration = TimeSpan.Zero;

        if (!_eventStartedAtCache.TryRemove(key, out startEventData, out var startedAt))
            return false;

        duration = timeStamp - startedAt;

        return true;
    }
}

public class EventTimer<TId> : EventTimer<TId, int>
    where TId : struct
{
    public void Start(TId key, DateTime timeStamp) => Start(key, 0, timeStamp);

    public bool TryStop(TId key, DateTime timeStamp, out TimeSpan duration) =>
        TryStop(key, timeStamp, out duration, out _);
}
#endif
