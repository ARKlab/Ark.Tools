using Microsoft.AspNetCore.Mvc;
using RavenDbSample.Models;
using Raven.Client.Documents.Session;
using System.Threading.Tasks;

namespace RavenDbSample.Controllers
{
	[ApiVersion("1.0")]
	[Route("AuditTest")]
	[ApiController]
	public class AuditTestController : ControllerBase
	{
		private readonly IAsyncDocumentSession _session;

		public AuditTestController(IAsyncDocumentSession session)
		{
			_session = session;
		}

		[HttpGet()]
		public async Task<IActionResult> TestDouble()
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

			//** Delete ***********************//
			_session.Delete(e);
			await _session.SaveChangesAsync();

			return Ok();
		}

		[HttpGet("{id}")]
		public async Task<IActionResult> TestUpdate([FromRoute]string id)
		{
			var e = new BaseOperation()
			{
				Id = null,
				B = new B()
				{
					Id = "B-single"
				}

			};

			//** Store ***********************//
			await _session.StoreAsync(e);
			await _session.SaveChangesAsync();

			//** Update ***********************//

			e.B.Id = "BUpdate";
			await _session.StoreAsync(e);
			await _session.SaveChangesAsync();

			//** Delete ***********************//
			_session.Delete(e);
			await _session.SaveChangesAsync();

			return Ok();
		}
	}
}
