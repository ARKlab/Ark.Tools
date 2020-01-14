using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.Core;
using Ark.Tools.Solid;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

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
		public async Task<IActionResult> Get(CancellationToken ctk = default(CancellationToken))
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			var res = Guid.NewGuid().ToString();

			var uri = Url.RouteUrl("V1.GetOperationById", new { operationId = res });

			var u_GetPathByName = _linkGenerator.GetPathByName("V1.GetOperationById", new { operationId = res });

			var u_GetPathByNameHttp = _linkGenerator.GetPathByName(this.HttpContext, "V1.GetOperationById", new { operationId = res });

			var pd = new RouteValueDictionary(new { operationId = res });
			pd.Add("api-version", "1.0");

			var u = Url.RouteUrl("V1.GetOperationById", pd);
			
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
