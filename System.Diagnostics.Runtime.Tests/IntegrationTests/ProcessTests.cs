using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

internal class Enabled_For_ProcessStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { ProcessEnabled = true };

    [Test]
    public Task Process_stats() =>
        InstrumentTest.Assert(measurements =>
            {
                Assert.That(() => measurements.LastValue("process.cpu.time"),
                    Is.GreaterThanOrEqualTo(0.01).After(2_000, 10));
                Assert.That(() => measurements.LastValue("process.cpu.usage"),
                    Is.GreaterThanOrEqualTo(0.01).After(2_000, 10));
                Assert.That(() => measurements.LastValue("process.cpu.count"),
                    Is.EqualTo(Environment.ProcessorCount).After(2_000, 10));
                Assert.That(() => measurements.LastValue("process.memory.usage"),
                    Is.GreaterThan(1000).After(2_000, 10));
                Assert.That(() => measurements.LastValue("process.memory.virtual"),
                    Is.GreaterThan(1000).After(2_000, 10));
                Assert.That(() => measurements.LastValue("process.handle.count"),
                    Is.GreaterThan(0).After(2_000, 10));
                Assert.That(() => measurements.LastValue("process.thread.count"),
                    Is.GreaterThan(0).After(2_000, 10));
            },
            "process.cpu.time", "process.cpu.usage", "process.cpu.count", "process.memory.usage",
            "process.memory.virtual", "process.handle.count", "process.thread.count");
}
