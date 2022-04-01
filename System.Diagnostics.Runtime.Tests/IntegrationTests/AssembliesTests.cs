using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

internal class Enabled_For_AssembliesStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { AssembliesEnabled = true };

    [Test]
    public Task Assemblies_stats()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Length;

        return InstrumentTest.Assert(measurements =>
            {
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}assembly.count"),
                    Is.GreaterThanOrEqualTo(assemblies).After(2_000, 10));
            },
            $"{Options.MetricPrefix}assembly.count");
    }
}
