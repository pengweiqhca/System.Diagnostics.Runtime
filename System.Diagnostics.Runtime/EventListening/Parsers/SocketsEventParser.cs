#if NET
using System.Diagnostics.Runtime.EventListening.Sources;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/net-5-new-networking-improvements/
/// </summary>
public class SocketsEventParser : EventCounterParserBase<SocketsEventParser>, SocketsEventParser.Events.CountersV5_0
{
#pragma warning disable CS0067
    [CounterName("outgoing-connections-established")]
    public event Action<MeanCounterValue>? OutgoingConnectionsEstablished;

    [CounterName("incoming-connections-established")]
    public event Action<MeanCounterValue>? IncomingConnectionsEstablished;

    [CounterName("bytes-sent")]
    public event Action<MeanCounterValue>? BytesSent;

    [CounterName("bytes-received")]
    public event Action<MeanCounterValue>? BytesReceived;

    [CounterName("datagrams-received")]
    public event Action<MeanCounterValue>? DatagramsReceived;

    [CounterName("datagrams-sent")]
    public event Action<MeanCounterValue>? DatagramsSent;
#pragma warning restore CS0067

    public override string EventSourceName => SocketsEventSource.Name;

    public static class Events
    {
        public interface CountersV5_0 : ICounterEvents
        {
            event Action<MeanCounterValue> OutgoingConnectionsEstablished;
            event Action<MeanCounterValue> IncomingConnectionsEstablished;
            event Action<MeanCounterValue> BytesSent;
            event Action<MeanCounterValue> BytesReceived;
            event Action<MeanCounterValue> DatagramsReceived;
            event Action<MeanCounterValue> DatagramsSent;
        }
    }
}
#endif
