using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RavenDbSample.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNet.OData;
using Raven.Client.Documents.Linq;
using RavenDbSample.Auditable;

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

		[HttpGet()]
		public async Task<IActionResult> Get()
		{
			var e = new BaseOperation()
			{
				Id = null,
				B = new B()
				{
					Id = "B-1"
				}
			
			};

			var e2 = new BaseOperation()
			{
				Id = null,
				B = new B()
				{
					Id = "B-2"
				}

			};

			//** Store ***********************//
			await _session.StoreAsync(e);
			await _session.StoreAsync(e2);
			await _session.SaveChangesAsync();



			//var audit = await _session.LoadAsync<Audit>("audits/417-A");

			//string changeVector = _session.Advanced.GetChangeVectorFor(audit);

			//var revisionsMetadata = await _session
			//.Advanced
			//.Revisions
			//.GetMetadataForAsync(audit.Id, start: 0, pageSize: 25);


			//var revisionBase = await _session.Advanced.Revisions.GetAsync<BaseOperation>("A:53-TttBA3x6AE6M/0uxkUIs2Q");
			//var revisionAudit = await _session.Advanced.Revisions.GetAsync<Audit>(changeVector);

			//var eRelated = await _session.LoadAsync<BaseOperation>(revisionAudit.EntityId);

			//** Delete ***********************//
			_session.Delete(e);
			await _session.SaveChangesAsync();
			
			return Ok();
		}
	}
}
