using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.Threading.Tasks;
using System.Collections.Generic;
using Raven.Client.Documents.Linq;
using Ark.Tools.RavenDb.Auditing;

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
		[Produces("application/json")]
		[ProducesResponseType(typeof(IEnumerable<Audit>), StatusCodes.Status200OK)]
		public async Task<IActionResult> Get(ODataQueryOptions<Audit> options)
		{
			var query = _session.Query<Audit>();
			var q2 = options.ApplyTo(query, new ODataQuerySettings
			{
				HandleNullPropagation = HandleNullPropagationOption.False
			}) as IRavenQueryable<Audit>;

			var set = await q2.ToListAsync();
			return Ok(set);
		}

		[HttpGet("{key}")]
		public async Task<IActionResult> Get([FromRoute]string key)
		{
			var operation = await _session.LoadAsync<Audit>(key);

			return Ok(operation);
		}
	}
}
