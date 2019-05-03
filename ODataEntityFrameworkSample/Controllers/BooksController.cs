using System.Linq;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using NodaTime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODataEntityFrameworkSample.Models;
using Ark.Tools.EntityFrameworkCore.SystemVersioning;
using Microsoft.AspNet.OData.Query;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace ODataEntityFrameworkSample.Controllers
{
	[ApiVersion("1.0")]
	[ODataRoutePrefix("Books")]
	//[Route("Books")]
	//[ApiController]
	//[ApiConventionType(typeof(DefaultApiConventions))] 

	public class BooksController : ODataController
	{
		private ODataSampleContext _db;

		public BooksController(ODataSampleContext context)
		{
			_db = context;

		}

		//[HttpGet]
		[ODataRoute]
		//[EnableQuery]
		[Produces("application/json")]
		[ProducesResponseType(typeof(ODataValue<IEnumerable<Book>>), StatusCodes.Status200OK)]
		[EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select)]
		public IActionResult Get()
		{
			return Ok(_db.Books);
		}

		[HttpGet("({key})")]
		[ODataRoute("({key})")]
		[EnableQuery]
		public IActionResult Get(int key)     //[FromODataUri] ????
		{
			if (key == 0)
			{
				var asOf = SystemClock.Instance.GetCurrentInstant();

				var bookList = _db
				  .Books
				  .SqlServerAsOf(asOf)
				  .ToList();

				return Ok(bookList);
			}

			return Ok(_db.Books.Find(key));
		}

		[HttpPost]
        [ODataRoute]
        [EnableQuery]
        public IActionResult Post([FromBody]Book book)
        {
            _db.Books.Add(book);
            _db.SaveChanges();
            return Created(book);
        }

        [HttpPatch("({key})")]
        [ODataRoute("({key})")]
        //[EnableQuery]
        public IActionResult Patch(int key, [FromBody] Delta<Book> book)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var entity = _db.Books.Find(key);
            if (entity == null)
            {
                return NotFound();
            }
            book.Patch(entity);
            try
            {
                _db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_booksExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(entity);
        }

        [HttpPut("({key})")]
        [ODataRoute("({key})")]
        //[EnableQuery]
        public IActionResult Put(int key, [FromBody] BookDto update)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (key != update.Id)
            {
                return BadRequest();
            }

			//Solution Working
			//var book = _db.Books.Find(key);
			//book.With(update);	
			//_db.Books.Update(book);

			//Clone Reflection is for Owned Collection Entity
			//OWNED_ENTITIES

			//Sol Reflection
			var book = _db.Books.Find(key);
			var be = _db.Entry(book);
			var ue = _db.Entry(update);
			be.CloneReflection(ue);
			_db.Books.Update(book);

			_db.Books.Persist().InsertOrUpdate()


			try
			{
                _db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_booksExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return Updated(update);
        }


		[HttpDelete("({key})")]
        [ODataRoute("({key})")]
        //[EnableQuery]
        public IActionResult Delete(int key)
        {
            var book = _db.Books.Find(key);
            if (book == null)
            {
                return NotFound();
            }
            _db.Books.Remove(book);
            _db.SaveChanges();
            return StatusCode((int)System.Net.HttpStatusCode.NoContent);
        }

        private bool _booksExists(int key)
        {
            return _db.Books.Any(x => x.Id == key);
        }


		//Sol 2 - NOT
		//var book = _db.Books.Find(key);

		//update._ETag = book._ETag;

		//(_db.Entry(book).Collection(p => p.Addresses).CurrentValue as ICollection<Address>).Clear();
		//_db.Entry(book).CurrentValues.SetValues(update);
		//_db.Entry(book).State = EntityState.Modified;

		//Sol 3 - NOT
		//_db.Entry(update).State = EntityState.Modified;
		//_db.UpdateImmutable(update);

		//Sol 4 
		//_db.Upsert(update);

		////Sol 5 NOT
		//var book = _db.Books.Find(key);
		//_db.Entry(book).CurrentValues.SetValues(update);
		//_db.Upsert(update);

		//Sol 6
		//_db.Attach(update);

		//var unchangedEntities = _db.ChangeTracker.Entries().Where(x => x.State == EntityState.Unchanged);

		//foreach (var ee in unchangedEntities)
		//{
		//	ee.State = EntityState.Modified;
		//}

		//Sol 7
		//var book = _db.Books.Find(key);
		//update._ETag = book._ETag;
		//_db.UpdateChildCollection(book, update, p => p.Addresses, collectionItem => collectionItem.Id);

		//Sol 8
		//var book = _db.Books.Find(key);

		//update._ETag = book._ETag;
		//_db.TestUpdate(update);

	}
}
