using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Mvc;
using RavenDbSample.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample.Controllers
{
	[ApiVersion("1.0")]
	[ODataRoutePrefix("Users")]
	public class UserController : ODataController
	{
		private readonly IAsyncDocumentSession _session;

		public UserController(IAsyncDocumentSession session)
		{
			_session = session;
		}

		[HttpGet]
		[ODataRoute]
		[EnableQuery]
		public async Task<IActionResult> Get(ODataQueryOptions<User> options)
		{
			var query = _session.Query<User>();
			var q2 = options.ApplyTo(query) as IQueryable<User>;
			var set = await q2.ToListAsync();
			return Ok(set);
		}

		[HttpGet("({key})")]
		[ODataRoute("({key})")]
		public async Task<IActionResult> Get([FromRoute][FromODataUri]string key)
		{
			var i = await _session.LoadAsync<User>(key);
			return Ok(i);
		}

		[HttpPost]
		[ODataRoute]
		public async Task<IActionResult> Post([FromBody] User u)
		{
			await _session.StoreAsync(u);
			await _session.SaveChangesAsync();
			return Ok(u);
		}
	}
}
