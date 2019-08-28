using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents.Session;
using System.Threading.Tasks;
using System.Collections.Generic;
using Ark.Tools.RavenDb.Auditing;
using Ark.Tools.AspNetCore.RavenDb;
using Ark.Tools.AspNetCore.Swashbuckle;

namespace RavenDbSample.Controllers
{
	[ApiVersion("1.0")]
	[Route("AuditController")]
	[ApiController]
	public class AuditController : ControllerBase
	{
		private readonly IAsyncDocumentSession _session;

		public AuditController(IAsyncDocumentSession session)
		{
			_session = session;
		}

		[HttpGet]
		[SwaggerAddODataParams]
		[Produces("application/json")]
		[ProducesResponseType(typeof(IEnumerable<Audit>), StatusCodes.Status200OK)]
		public async Task<IActionResult> Get(ODataQueryOptions<Audit> options)
		{
			var validations = new RavenDefaultODataValidationSettings()
			{
				AllowedOrderByProperties =
				{
					"LastUpdatedUtc"
				},
			};

			var res = await _session.Query<Audit>().GetPagedWithODataOptions<Audit>(options, validations);

			return Ok(res);
		}

		[HttpGet("{key}")]
		public async Task<IActionResult> Get([FromRoute]string key)
		{
			var operation = await _session.LoadAsync<Audit>(key);

			return Ok(operation);
		}
	}
}
