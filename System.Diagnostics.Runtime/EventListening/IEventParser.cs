using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening;

/// <summary>
/// A <see cref="IEventListener"/> that receives "untyped" events of <see cref="EventWrittenEventArgs"/> into strongly-typed events.
/// </summary>
public interface IEventParser<TEvents> : IEventListener
    where TEvents : IEvents
{
}
