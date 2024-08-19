using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents.Session;
using System.Threading.Tasks;
using System.Collections.Generic;
using Ark.Tools.RavenDb.Auditing;
using Ark.Tools.AspNetCore.RavenDb;
using Ark.Tools.Solid;
using Ark.Tools.Core;
using RavenDbSample.Models;
using System.Threading;
using RavenDbSample.Application.Requests;

namespace RavenDbSample.Controllers
{
	[ApiVersion("1.0")]
	[Route("ContractsController")]
	[ApiController]
	public class ContractsController : ControllerBase
	{
		private readonly IQueryProcessor _queryProcessor;
		private readonly IRequestProcessor _requestProcessor;

		public ContractsController(IQueryProcessor queryProcessor, IRequestProcessor requestProcessor)
		{
			_queryProcessor = queryProcessor;
			_requestProcessor = requestProcessor;
		}

		/// <summary>
		/// Create a contract
		/// </summary>
		/// <param name="blId">business line ID</param>
		/// <param name="contract">The contract to be created</param>
		/// <param name="ctk">Cancellation Token</param>
		/// <returns></returns>
		[HttpPost]
		[ProducesResponseType(typeof(Contract.Output), StatusCodes.Status200OK)]
		public async Task<IActionResult> Post([FromRoute]string blId, [FromBody] Contract.Input contract, CancellationToken ctk = default)
		{
			contract.BusinessLineId = blId;

			var request = new Contract_CreateRequest.V1()
			{
				Entity = contract
			};

			var res = await _requestProcessor.ExecuteAsync(request, ctk);

			return Ok(res);
		}
	}
}
