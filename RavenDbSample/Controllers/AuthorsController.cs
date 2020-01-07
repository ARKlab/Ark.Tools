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
	[Route("Authors")]
	[ApiController]
	public class AuthorsController : ControllerBase
	{
		private readonly IAsyncDocumentSession _session;

		public AuthorsController(IAsyncDocumentSession session)
		{
			_session = session;
		}

		//[HttpGet]
		//[Produces("application/json")]
		//[ProducesResponseType(typeof(IEnumerable<Author>), StatusCodes.Status200OK)]
		//public async Task<IActionResult> Get(ODataQueryOptions<Author> options)
		//{
		//	var query = _session.Query<Author>();
		//	var q2 = options.ApplyTo(query, new ODataQuerySettings
		//	{
		//		HandleNullPropagation = HandleNullPropagationOption.False
		//	}) as IRavenQueryable<Author>;

		//	var set = await q2.ToListAsync();
		//	return Ok(set);
		//}

		[HttpGet("{key}")]
		[Produces("application/json")]
		[ProducesResponseType(typeof(Author), StatusCodes.Status200OK)]
		public async Task<IActionResult> Get([FromRoute]string key)
		{
			var operation = await _session.LoadAsync<Author>(key);

			if (operation == null)
				return NotFound();

			return Ok(operation);
		}


		[HttpPost]
		[Consumes("application/json")]
		[Produces("application/json")]
		[ProducesResponseType(typeof(Author), StatusCodes.Status200OK)]
		public async Task<IActionResult> Post([FromBody] Author operationBase)
		{
			await _session.StoreAsync(operationBase);
			await _session.SaveChangesAsync();

			return Ok(operationBase);
		}

		[HttpPut("{id}/bookId")]
		[Consumes("application/json")]
		[Produces("application/json")]
		[ProducesResponseType(typeof(Author), StatusCodes.Status200OK)]
		public async Task<IActionResult> Add_Book(string id, [FromQuery] string bookId)
		{
			var a = await _session.LoadAsync<Author>(id);
			
			if (!a.BookIds.Contains(bookId))
			{
				a.BookIds.Add(bookId);
				await _session.SaveChangesAsync();
			}

			return Ok(a);
		}

		[HttpDelete("{id}/bookId")]
		[Consumes("application/json")]
		[Produces("application/json")]
		[ProducesResponseType(typeof(Author), StatusCodes.Status200OK)]
		public async Task<IActionResult> Remove_Book(string id, [FromQuery] string bookId)
		{
			var a = await _session.LoadAsync<Author>(id);

			if (a.BookIds.Contains(bookId))
			{
				a.BookIds.Remove(bookId);
				await _session.SaveChangesAsync();
			}

			return Ok(a);
		}
	}
}
