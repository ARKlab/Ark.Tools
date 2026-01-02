using Asp.Versioning;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestWithoutArkTools.Controllers
{
    [ApiVersion("1.0")]
    [Route("test")]
    public class TestController : ApiController
    {
        private readonly ILogger<TestController> _logger;
        private readonly LinkGenerator _linkGenerator;

        public TestController(ILogger<TestController> logger, LinkGenerator linkGenerator)
        {
            _logger = logger;
            _linkGenerator = linkGenerator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(OutputObject), 200)]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IActionResult> Get(CancellationToken ctk = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var res = Guid.NewGuid().ToString();

            var ambients = this.HttpContext?.Features.Get<IRouteValuesFeature>()?.RouteValues;

            var rd = new RouteValueDictionary(new { operationId = res })
            {
                { "api-version", ambients?["api-version"] }
            };

            var rd2 = new RouteValueDictionary(new { operationId = res });
            rd2.Add("api-version", ambients?["api-version"]);

            var uri = this.Url.Link("V1.GetOperationById", rd);

            var u_GetPathByName = _linkGenerator.GetPathByName("V1.GetOperationById", new { operationId = res });

            var u_GetPathByNameHttp = _linkGenerator.GetPathByName(this.HttpContext!, "V1.GetOperationById", rd);

            var u = Url.RouteUrl("V1.GetOperationById", rd);

            return this.Ok(new OutputObject()
            {
                LinkUrl = uri,
                GetPathByName = u_GetPathByName,
                GetPathByNameHttpContext = u_GetPathByNameHttp,
                Test = u
            });
        }
    }
}