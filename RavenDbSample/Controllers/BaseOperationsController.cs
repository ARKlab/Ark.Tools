using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RavenDbSample.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.Threading.Tasks;
using System.Collections.Generic;
using Raven.Client.Documents.Linq;

namespace RavenDbSample.Controllers
{
	[ApiVersion("1.0")]
	[Route("BaseOperations")]
	[ApiController]
	public class BaseOperationsController : ControllerBase
	{
		private readonly IAsyncDocumentSession _session;

		public BaseOperationsController(IAsyncDocumentSession session)
		{
			_session = session;
		}

		[HttpGet]
		//[Produces("application/json")]
		[ProducesResponseType(typeof(IEnumerable<BaseOperation>), StatusCodes.Status200OK)]
		//[EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select)]
		public async Task<IActionResult> Get(ODataQueryOptions<BaseOperation> options)
		{
			var query = _session.Query<BaseOperation>();
			var q2 = options.ApplyTo(query, new ODataQuerySettings
			{
				HandleNullPropagation = HandleNullPropagationOption.False
			}) as IRavenQueryable<BaseOperation>;

			var set = await q2.ToListAsync();
			return Ok(set);
		}

		[HttpGet("{key}")]
		//[Produces("application/json")]
		[ProducesResponseType(typeof(BaseOperation), StatusCodes.Status200OK)]
		public async Task<IActionResult> Get([FromRoute]string key)
		{
			var operation = await _session.LoadAsync<BaseOperation>(key);

			if (operation == null)
				return NotFound();

			return Ok(operation);
		}


		[HttpPost]
		//[Consumes("application/json")]
		//[Produces("application/json")]
		[ProducesResponseType(typeof(BaseOperation), StatusCodes.Status200OK)]
		public async Task<IActionResult> Post([FromBody] BaseOperation operationBase)
		{
			await _session.StoreAsync(operationBase);
			await _session.SaveChangesAsync();

			return Ok(operationBase);
		}
	}
}
