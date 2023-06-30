using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetFrameworkExample;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using System.Diagnostics.Runtime;

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
            .AddRuntimeInstrumentation()
            .AddView(instrument =>
                instrument.Name is "process.runtime.dotnet.gc.collection.seconds" or "process.runtime.dotnet.gc.pause.seconds"
                    ? new ExplicitBucketHistogramConfiguration { Boundaries = new[] { 0.001, 0.01, 0.05, 0.1, 0.5, 1, 10 } }
                    : null)
            .AddPrometheusHttpListener()
            .Build()!))
    .Build().RunAsync().ConfigureAwait(false);
