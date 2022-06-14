using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetFrameworkExample;
using System.Diagnostics.Runtime;
using OpenTelemetry.Metrics;

await new HostBuilder().ConfigureServices(services => services
        .AddHostedService<HttpListenerHostedService>()
        .AddOpenTelemetryMetrics(builder => builder.AddRuntimeInstrumentation().AddView(instrument =>
                instrument.Name is "process.runtime.dotnet.gc.collection.seconds" or
                    "process.runtime.dotnet.gc.pause.seconds"
                    ? new ExplicitBucketHistogramConfiguration
                        { Boundaries = new[] { 0.001, 0.01, 0.05, 0.1, 0.5, 1, 10 } }
                    : null)
            .AddPrometheusExporter()))
    .Build().RunAsync().ConfigureAwait(false);
