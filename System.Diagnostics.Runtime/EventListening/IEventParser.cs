using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening;

/// <summary>
/// A <see cref="IEventListener"/> that receives "untyped" events of <see cref="EventWrittenEventArgs"/> into strongly-typed events.
/// </summary>
/// <typeparam name="TEvents">
/// Represents the set of strongly-typed events emitted by this parser. Implementors should not directly implement <see cref="IEvents"/>, rather
/// implement inheriting interfaces such as <see cref="IInfoEvents"/>, <see cref="IWarningEvents"/>, etc.
/// </typeparam>
public interface IEventParser<TEvents> : IEventListener
    where TEvents : IEvents
{
}
