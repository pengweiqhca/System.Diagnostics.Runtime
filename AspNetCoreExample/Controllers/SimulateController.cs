using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Runtime;

namespace AspNetCoreExample.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SimulateController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> Get(
        bool simulateAlloc = true,
        bool simulateContention = true,
        bool simulateJit = true,
        bool simulateException = true,
        bool simulateBlocking = true,
        bool simulateOutgoingNetwork = true)
    {
        await Simulate.Invoke(simulateAlloc, simulateContention, simulateJit, simulateException, simulateBlocking,
                simulateOutgoingNetwork ? httpClientFactory.CreateClient : null)
            .ConfigureAwait(false);

        return new[] { "value1", "value2" };
    }
}
