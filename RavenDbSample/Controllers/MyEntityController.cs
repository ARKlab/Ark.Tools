using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Ark.Tools.Solid;
using NLog;
using System.Threading;
using Ark.Tools.EventSourcing.Store;
using RavenDbSample.Models;

namespace RavenDbSample.Controllers
{
	[ApiVersion("1.0")]
	[Route("MyEntity")]
	[ApiController]
	public class MyEntityController : ControllerBase
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();
		private readonly IAggregateTransactionFactory<MyEntityAggregate, MyEntityState, MyEntity> _transactionFactory;

		public MyEntityController(IAggregateTransactionFactory<MyEntityAggregate, MyEntityState, MyEntity> transactionFactory)
		{
			_transactionFactory = transactionFactory;
		}

		/// <summary>
		/// Get import State by id
		/// </summary>
		/// <param name="id">ID of import state</param>
		/// <param name="ctk">Cancellation Token</param>
		/// <returns></returns>
		[HttpGet("{id}")]
		[Produces("application/json")]
		[ProducesResponseType(typeof(IEnumerable<MyEntity>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetById([FromRoute]string id, CancellationToken ctk = default)
		{
			_logger.Trace($@"Get: Import id: {id}");

			var res = await _transactionFactory.LoadCapturedState(id, ctk);

			return Ok(res);
		}
	}
}
