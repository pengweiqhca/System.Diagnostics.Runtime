using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.Util;

/// <summary>
/// To generate metrics, we are often interested in the duration between two events. This class
/// helps time the duration between two events.
/// </summary>
/// <typeparam name="TId">A type of an identifier present on both events</typeparam>
/// <typeparam name="TEventData">A struct that represents data of interest extracted from the first event</typeparam>
public class EventPairTimer<TId, TEventData>
    where TId : struct
    where TEventData : struct
{
    private readonly Cache<TId, TEventData> _eventStartedAtCache;
    private readonly int _startEventId;
    private readonly int _endEventId;
    private readonly Func<EventWrittenEventArgs, TId> _extractEventIdFn;
    private readonly Func<EventWrittenEventArgs, TEventData> _extractData;

    public EventPairTimer(
        int startEventId,
        int endEventId,
        Func<EventWrittenEventArgs, TId> extractEventIdFn,
        Func<EventWrittenEventArgs, TEventData> extractData,
        Cache<TId, TEventData>? cache = null)
    {
        _startEventId = startEventId;
        _endEventId = endEventId;
        _extractEventIdFn = extractEventIdFn;
        _extractData = extractData;
        _eventStartedAtCache = cache ?? new Cache<TId, TEventData>(TimeSpan.FromMinutes(1));
    }

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

        if (e.EventId == _startEventId)
        {
#if NETCOREAPP
            _eventStartedAtCache.Set(_extractEventIdFn(e), startEventData = _extractData(e), e.TimeStamp);
#else
            _eventStartedAtCache.Set(_extractEventIdFn(e), startEventData = _extractData(e), DateTime.Now);
#endif
            return DurationResult.Start;
        }

        if (e.EventId != _endEventId) return DurationResult.Unrecognized;

        if (!_eventStartedAtCache.TryRemove(_extractEventIdFn(e), out startEventData, out var timeStamp))
            return DurationResult.FinalWithoutDuration;
#if NETCOREAPP
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

public sealed class EventPairTimer<TId> : EventPairTimer<TId, int>
    where TId : struct
{
    public EventPairTimer(int startEventId, int endEventId, Func<EventWrittenEventArgs, TId> extractEventIdFn, Cache<TId, int>? cache = null)
        : base(startEventId, endEventId, extractEventIdFn, _ => 0, cache)
    {
    }

    public DurationResult TryGetDuration(EventWrittenEventArgs e, out TimeSpan duration) =>
        TryGetDuration(e, out duration, out _);
}
#if NETFRAMEWORK
internal class EventTimer<TId, TData>
    where TId : struct
    where TData : struct
{
    private readonly Cache<TId, TData> _eventStartedAtCache;

    public EventTimer(Cache<TId, TData>? cache = null) =>
        _eventStartedAtCache = cache ?? new(TimeSpan.FromMinutes(1));

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

internal class EventTimer<TId> : EventTimer<TId, int>
    where TId : struct
{
    public void Start(TId key, DateTime timeStamp) => Start(key, 0, timeStamp);

    public bool TryStop(TId key, DateTime timeStamp, out TimeSpan duration) =>
        TryStop(key, timeStamp, out duration, out _);
}
#endif
