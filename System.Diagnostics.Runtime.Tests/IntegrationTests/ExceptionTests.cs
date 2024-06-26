using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

[TestFixture]
internal class Given_Exception_Events_Are_Enabled_For_Exception_Stats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { ExceptionsEnabled = true };

    [Test]
    [MaxTime(10_000)]
    public Task Will_measure_when_occurring_an_exception()
    {
        const int numToThrow = 10;

        return InstrumentTest.Assert(() =>
            {
                var divider = 0;

                for (var i = 0; i < numToThrow; i++)
                    try
                    {
                        _ = 1 / divider;
                    }
                    catch (DivideByZeroException)
                    {
                    }
            }, measurements =>
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}exception_types.count", "type", "System.DivideByZeroException"),
                    Is.GreaterThanOrEqualTo(numToThrow).After(5000, 10)),
            $"{Options.MetricPrefix}exception_types.count");
    }
}
