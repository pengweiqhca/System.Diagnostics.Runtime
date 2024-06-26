using System.Diagnostics.Metrics;
using NUnit.Framework;
using OpenTelemetry.Metrics;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

[TestFixture]
public abstract class IntegrationTestBase
{
    private TestMeterProviderBuilder? _builder;
    private readonly Meter _meter = new("TestMeter");

    protected RuntimeMetricsOptions Options { get; private set; } = default!;

    [SetUp]
    public void SetUp()
    {
        Options = GetOptions();
#if NETFRAMEWORK
        Options.EtwSessionName = "System.Diagnostics.Runtime.Tests.IntegrationTests";
#endif
        _builder = new();

        _builder.AddProcessRuntimeInstrumentation(Options);
    }

    private sealed class TestMeterProviderBuilder : MeterProviderBuilder, IDisposable
    {
        private readonly List<IDisposable> _disposables = [];

        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<TInstrumentation> instrumentationFactory)
        {
            if (instrumentationFactory() is IDisposable disposable)
                _disposables.Add(disposable);

            return this;
        }

        public override MeterProviderBuilder AddMeter(params string[] names) => this;

        public void Dispose()
        {
            foreach (var disposable in _disposables) disposable.Dispose();
        }
    }

    [TearDown]
    public void TearDown()
    {
        _builder?.Dispose();

        _meter.Dispose();
    }

    protected abstract RuntimeMetricsOptions GetOptions();
}
