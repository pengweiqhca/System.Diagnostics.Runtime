using System.Diagnostics.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetFrameworkExample;
using OpenTelemetry;
using OpenTelemetry.Metrics;

_ = Task.Run(() =>
{
    var obj = new object();

    while (true)
        lock (obj)
            _ = DateTime.Now;
});

await new HostBuilder().ConfigureServices(services => services
        .AddHostedService<HttpListenerHostedService>()
        .AddSingleton(Sdk.CreateMeterProviderBuilder()
            .AddExampleInstrumentation()
            .AddView(instrument =>
                instrument.Name is "process.runtime.dotnet.gc.collections.duration"
                    or "process.runtime.dotnet.gc.pause.duration"
                    ? new ExplicitBucketHistogramConfiguration { Boundaries = [1, 10, 50, 100, 500, 1000, 10000] }
                    : null)
            .AddPrometheusHttpListener()
            .Build()))
    .Build().RunAsync().ConfigureAwait(false);
