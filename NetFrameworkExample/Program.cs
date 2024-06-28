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
            .AddPrometheusHttpListener()
            .Build()))
    .Build().RunAsync().ConfigureAwait(false);
