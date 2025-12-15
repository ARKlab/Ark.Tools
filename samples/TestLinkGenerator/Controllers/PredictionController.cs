using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System.Threading;
using System.Threading.Tasks;

namespace TestWithoutArkTools.Controllers
{
    [ApiVersion("1.0")]
    [Route("prediction")]
    public class PredictionController : ApiController
    {
        private readonly ILogger<PredictionController> _logger;

        public PredictionController(ILogger<PredictionController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the Operation by Id
        /// </summary>
        /// <remarks></remarks>
        /// <response code="200">The returning operation</response>
        [HttpGet("{operationId}", Name = "V1.GetOperationById")]
        [ProducesResponseType(typeof(string), 200)]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IActionResult> GetOperationById([FromRoute] string operationId, CancellationToken ctk = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string? res = null;// "Prova";

#pragma warning disable CA1508 // Avoid dead conditional code
            if (res != null)
                return this.Ok(res);
#pragma warning restore CA1508 // Avoid dead conditional code

            return this.NotFound();
        }

    }
}
