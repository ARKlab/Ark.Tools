using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Controllers
{
	//[ApiVersion("1.0")]
	//[Route("Operations")]
	//public class OperationsController : ControllerBase
	//{
	//	private readonly IAsyncDocumentSession _session;

	//	public OperationsController(IAsyncDocumentSession session)
	//	{
	//		_session = session;
	//	}

	//	[HttpGet]
	//	public async Task<IActionResult> Get(ODataQueryOptions<BaseOperation> options)
	//	{
	//		var query = _session.Query<BaseOperation>();
	//		var q2 = options.ApplyTo(query) as IQueryable<BaseOperation>;
	//		var set = await q2.ToListAsync();
	//		return Ok(set);
	//	}

	//	//[HttpGet("{key}")]
	//	//public async Task<IActionResult> Get([FromRoute][FromODataUri]string key)
	//	//{
	//	//	var i = await _session.LoadAsync<BaseOperation>(key);
	//	//	return Ok(i);
	//	//}

	//	[HttpPost]
	//	[Consumes("application/json")]
	//	[Produces("application/json")]
	//	[ProducesResponseType(typeof(BaseOperation), StatusCodes.Status200OK)]
	//	public async Task<IActionResult> Post([FromBody] BaseOperation operationBase)
	//	{
	//		var a = new A();
	//		var b = new B();

	//		operationBase.Operations = new List<Base>();
	//		operationBase.Operations.Add(a);
	//		operationBase.Operations.Add(b);

	//		await _session.StoreAsync(operationBase);
	//		await _session.SaveChangesAsync();

	//		return Ok(operationBase);
	//	}
	//}

	[ApiVersion("1.0")]
	[ODataRoutePrefix("Operations")]
	public class OperationsController : ODataController
	{
		private readonly IAsyncDocumentSession _session;

		public OperationsController(IAsyncDocumentSession session)
		{
			_session = session;
		}

		[HttpGet]
		public async Task<IActionResult> Get(ODataQueryOptions<BaseOperation> options)
		{
			var query = _session.Query<BaseOperation>();
			var q2 = options.ApplyTo(query) as IQueryable<BaseOperation>;
			var set = await q2.ToListAsync();
			return Ok(set);
		}

		[HttpGet("{key}")]
		public async Task<IActionResult> Get([FromRoute][FromODataUri]string key)
		{
			var ops = await _session.LoadAsync<BaseOperation>(key);

			//foreach (var item in ops.Operations)
			//{
			//	var s = await _session.LoadAsync<Base>(item.Id);
			//}


			return Ok(ops);
		}

		//[HttpGet("{key}/Base")]
		//public async Task<IActionResult> GetBase([FromRoute][FromODataUri]string key)
		//{
		//	var i = await _session.LoadAsync<Base>(key);
		//	return Ok(i);
		//}

		[HttpPost]
		[Consumes("application/json")]
		[Produces("application/json")]
		[ProducesResponseType(typeof(BaseOperation), StatusCodes.Status200OK)]
		public async Task<IActionResult> Post([FromBody] BaseOperation operationBase)
		{
			var a = new A()
			{
				Id = "Id-A",
				ValueFromA = 10,
			};

			var b = new B()
			{
				Id = "Id-B",
				ValueFromB = 20,
			};

			operationBase.Id = "Op1";
			//operationBase.Operations = new List<Base>();
			//operationBase.Operations.Add(a);
			//operationBase.Operations.Add(b);

			//await _session.StoreAsync(a);
			//await _session.StoreAsync(b);
			await _session.StoreAsync(operationBase);

			await _session.SaveChangesAsync();

			return Ok(operationBase);
		}
	}
}
