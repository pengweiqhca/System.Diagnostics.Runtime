using System.Diagnostics.Runtime;
using System.Net;
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddRuntimeInstrumentation()
    .AddView(instrument =>
        instrument.Name is "process.runtime.dotnet.gc.collection.seconds" or "process.runtime.dotnet.gc.pause.seconds"
            ? new ExplicitBucketHistogramConfiguration { Boundaries = new[] { 0.001, 0.01, 0.05, 0.1, 0.5, 1, 10 } }
            : null)
    .AddPrometheusExporter()
    .Build();

using var httpListener = new HttpListener();

httpListener.Prefixes.Add("http://localhost:9465/");

Task.Run(async () =>
{
    httpListener.Start();

    var client = new HttpClient();

    while (true)
    {
        var context = await httpListener.GetContextAsync().ConfigureAwait(false);

        _ = Task.Run(async () =>
        {
            await Simulate.Invoke(true, true, true, true, () => client).ConfigureAwait(false);

            context.Response.Close();
        });
    }
});

Console.WriteLine("Presse ENTER to exit...");

Console.ReadLine();
