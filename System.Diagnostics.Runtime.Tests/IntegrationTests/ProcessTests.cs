using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

internal class Enabled_For_ProcessStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { ProcessEnabled = true };

    [Test]
    public Task Process_stats() => InstrumentTest.Assert(measurements =>
    {
        Assert.That(() => measurements.LastValue("process.cpu.utilization"),
            Is.GreaterThanOrEqualTo(0.01).After(2_000, 10));
        Assert.That(() => measurements.LastValue("process.handle.count"),
            Is.GreaterThan(0).After(2_000, 10));
    }, "process.cpu.utilization", "process.handle.count");
}
