#if NET
using System.Diagnostics.Runtime.EventListening.Sources;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/net-5-new-networking-improvements/
/// </summary>
public class NameResolutionEventParser : EventCounterParserBase<NameResolutionEventParser>, NameResolutionEventParser.Events.CountersV5_0
{
#pragma warning disable CS0067
    [CounterName("dns-lookups-requested")] public event Action<MeanCounterValue>? DnsLookupsRequested;

    [CounterName("current-dns-lookups")] public event Action<MeanCounterValue>? CurrentDnsLookups;

    [CounterName("dns-lookups-duration")] public event Action<MeanCounterValue>? DnsLookupsDuration;
#pragma warning restore CS0067

    public override string EventSourceName => NameResolutionEventSource.Name;

    public static class Events
    {
        public interface CountersV5_0 : ICounterEvents
        {
            event Action<MeanCounterValue> DnsLookupsRequested;
            event Action<MeanCounterValue> CurrentDnsLookups;
            event Action<MeanCounterValue> DnsLookupsDuration;
        }
    }
}
#endif
