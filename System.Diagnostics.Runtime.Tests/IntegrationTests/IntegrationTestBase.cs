using System.Diagnostics.Metrics;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

[TestFixture]
public abstract class IntegrationTestBase
{
    private RuntimeInstrumentation? _instrumentation;
    private readonly Meter _meter = new("TestMeter");

    protected RuntimeMetricsOptions Options { get; private set; } = default!;

    [SetUp]
    public void SetUp()
    {
        Options = GetOptions();
#if NETFRAMEWORK
        Options.EtwSessionName = "System.Diagnostics.Runtime.Tests.IntegrationTests";
#endif
        _instrumentation = new RuntimeInstrumentation(Options);
    }

    [TearDown]
    public void TearDown()
    {
        _instrumentation?.Dispose();

        _meter.Dispose();
    }

    protected abstract RuntimeMetricsOptions GetOptions();
}
