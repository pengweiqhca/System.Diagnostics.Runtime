using Microsoft.Extensions.Hosting;
using System.Diagnostics.Runtime;
using System.Net;

namespace NetFrameworkExample;

public class HttpListenerHostedService : BackgroundService, IDisposable
{
    private readonly HttpListener _httpListener = new() { Prefixes = { "http://localhost:9465/" } };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _httpListener.Start();

        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        while (!stoppingToken.IsCancellationRequested)
        {
            var context = await _httpListener.GetContextAsync().ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                await Simulate.Invoke(true, true, true, true, true, () => client).ConfigureAwait(false);

                context.Response.Close();
            }, stoppingToken);
        }
    }
}
