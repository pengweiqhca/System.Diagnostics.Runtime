using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

#if NET6_0_OR_GREATER
// TODO need to test incoming network activity
public class SocketsTests : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { SocketsEnabled = true };

    [Test]
    public Task Given_A_HTTP_Request_Then_Outgoing_metrics_Should_Increase() =>
        InstrumentTest.Assert(async () =>
        {
            // arrange
            using var client = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.MaxValue,
                MaxConnectionsPerServer = 10
            });

            await Task.WhenAll(Enumerable.Range(1, 20)
                .Select(_ => client.GetAsync("https://httpstat.us/200?sleep=3000"))).ConfigureAwait(false);
        }, measurements =>
        {
            // assert
            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}sockets.connections.established.outgoing.total"),
                Is.GreaterThanOrEqualTo(10).After(2_000, 100));
            Assert.That(measurements.Sum($"{Options.MetricPrefix}sockets.bytes.sent.total"), Is.GreaterThan(0));
        }, $"{Options.MetricPrefix}sockets.connections.established.outgoing.total",
            $"{Options.MetricPrefix}sockets.bytes.sent.total");
}
#endif
