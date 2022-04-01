using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Runtime.EventListening;

namespace System.Diagnostics.Runtime;

public interface IConsumes<out TEvents>
    where TEvents : IEvents
{
    public TEvents? Events { get; }

    /// <summary>
    /// Indicates if the events of <typeparamref name="TEvents"/> will be produced and can be listened to.
    /// </summary>
    /// <remarks>
    /// As event parsers may or may not be enabled (or enabled at lower event levels), we need a mechanism to indicate if
    /// events are available or not to generate metrics from.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Events))]
    public bool Enabled { get; }
}

internal class EventConsumer<T> : IConsumes<T>
    where T : IEvents
{
    public EventConsumer()
    {
        Enabled = false;
    }

    public EventConsumer(T? events)
    {
        Events = events;
        Enabled = events != null;
    }

    public T? Events { get; }

    [MemberNotNullWhen(true, nameof(Events))]
    public bool Enabled { get; set; }
}
