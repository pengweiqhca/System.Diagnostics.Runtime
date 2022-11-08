using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;
#if NET
internal class Enabled_For_JitStatsTest : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { JitEnabled = true };

    [Test]
    public Task When_Running_On_NET50_Then_Counts_Of_Methods_Are_Recorded() =>
        InstrumentTest.Assert(() => RuntimeEventHelper.CompileMethods(() => 1), measurements =>
            {
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}jit.il.bytes.total"), Is.GreaterThan(0).After(2_000, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}git.method.total"), Is.GreaterThan(0 + 100).After(2_000, 10));
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}jit.time.total"), Is.GreaterThan(0).After(2_000, 10));
            }, $"{Options.MetricPrefix}jit.il.bytes.total",
            $"{Options.MetricPrefix}git.method.total",
            $"{Options.MetricPrefix}jit.time.total");
}
#endif
