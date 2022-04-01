using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreExample.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NoOverheadController : ControllerBase
    {
        [HttpGet]
#pragma warning disable CS1998
        public async Task<ActionResult<IEnumerable<string>>> Get2()
#pragma warning restore CS1998
        {
            return new [] {"value1", "value2"};
        }
    }
}
