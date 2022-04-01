using System.Diagnostics.Metrics;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

[TestFixture]
public abstract class IntegrationTestBase
{
    private RuntimeInstrumentation _instrumentation = default!;
    private readonly Meter _meter = new ("TestMeter");

    protected RuntimeMetricsOptions Options { get; private set; } = default!;

    [SetUp]
    public void SetUp() => _instrumentation = new RuntimeInstrumentation(Options = GetOptions());

    [TearDown]
    public void TearDown()
    {
        _instrumentation.Dispose();

        _meter.Dispose();
    }

    protected abstract RuntimeMetricsOptions GetOptions();
}
