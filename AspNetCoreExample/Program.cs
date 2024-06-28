using System.Diagnostics.Runtime;
using OpenTelemetry;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMvc();

builder.Services.AddHttpClient();

builder.Services.AddSingleton(Sdk.CreateMeterProviderBuilder()
    .AddExampleInstrumentation()
    .AddPrometheusExporter()
    .Build());

var app = builder.Build();

app.UseDeveloperExceptionPage();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseRouting();

app.Use((context, next) =>
{
    if (context.Request.Path != "/") return next();

    context.Response.Redirect("/metrics");

    return Task.CompletedTask;
});

app.MapControllers();

_ = Task.Run(() =>
{
    var obj = new object();

    while (true)
        lock (obj)
            _ = DateTime.Now;
});

app.Run();
