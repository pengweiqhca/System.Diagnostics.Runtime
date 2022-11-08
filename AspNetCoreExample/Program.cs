using OpenTelemetry;
using OpenTelemetry.Metrics;
using Prometheus;
using System.Diagnostics.Runtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMvc();

builder.Services.AddHttpClient();

builder.Services.AddSingleton(Sdk.CreateMeterProviderBuilder()
    .AddRuntimeInstrumentation()
    .AddView(instrument =>
        instrument.Name is "process.runtime.dotnet.gc.collection.seconds" or "process.runtime.dotnet.gc.pause.seconds"
            ? new ExplicitBucketHistogramConfiguration { Boundaries = new[] { 0.001, 0.01, 0.05, 0.1, 0.5, 1, 10 } }
            : null)
    .AddPrometheusExporter()
    .Build()!);

var app = builder.Build();

app.UseDeveloperExceptionPage();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseMetricServer("/prometheus");

app.UseRouting();

app.Use((context, next) =>
{
    if (context.Request.Path != "/") return next();

    context.Response.Redirect("/metrics");

    return Task.CompletedTask;
});

app.MapControllers();

Prometheus.DotNetRuntime.DotNetRuntimeStatsBuilder.Default().StartCollecting();

Task.Run(() =>
{
    var obj = new object();

    while (true)
        lock (obj)
            _ = DateTime.Now;
});

app.Run();
