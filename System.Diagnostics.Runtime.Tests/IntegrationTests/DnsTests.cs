using System.Net;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

#if NET6_0_OR_GREATER
public class Enabled_For_DnsTests : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { DnsEnabled = true };

    [Test]
    public Task Given_A_HTTP_Request_Then_Outgoing_metrics_Should_Increase() =>
        InstrumentTest.Assert(() => Dns.GetHostAddressesAsync("www.google.com"), measurements =>
        {
            // assert
            Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}dns.requested.total"),
                Is.GreaterThanOrEqualTo(1).After(2_000, 100));
            Assert.That(measurements.LastValue($"{Options.MetricPrefix}dns.current.count"), Is.GreaterThanOrEqualTo(0));
            Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}dns.duration.total"),
                Is.GreaterThan(0).After(2_000, 100));
        }, $"{Options.MetricPrefix}dns.requested.total",
            $"{Options.MetricPrefix}dns.current.count",
            $"{Options.MetricPrefix}dns.duration.total");
}
#endif
