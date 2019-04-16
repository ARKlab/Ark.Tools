using System.Linq;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using NodaTime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODataSample.Models;
using Ark.Tools.EntityFrameworkCore.SystemVersioning;
using Microsoft.AspNet.OData.Query;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace ODataSample.Controllers
{
	[ApiVersion("1.0")]
	[ODataRoutePrefix("Books")]
	//[Route("Books")]
	//[ApiController]
	//[ApiConventionType(typeof(DefaultApiConventions))] 

	public class BooksController : ODataController
	{
		private BookStoreContext _db;

		public BooksController(BookStoreContext context)
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

		//************************************* TEST **********************************************************//
		[HttpGet("({myKey})")]
		[ODataRoute("({myKey})")]
		[EnableQuery]
		public IActionResult GetByString(string myKey)    
		{
			return Ok();
		}

		//[HttpGet("Books(Id={Id}, Year={Year})")]
		//[ODataRoute("Books(Id={Id}, Year={Year})")]
		//[EnableQuery]
		//public IActionResult TestWithTwoParameters([FromODataUri]int Id, [FromODataUri]int Year)
		//{
		//	return Ok();
		//}

		//[HttpGet("GetReports(Id={Id},Year={Year})")]
		//[ODataRoute("GetReports(Id={Id},Year={Year})")]
		//[EnableQuery]
		//public IActionResult TestWithTwoParameters([FromODataUri]int Id, [FromODataUri]int Year)
		//{
		//	return Ok();
		//}
		//*****************************************************************************************************//

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
        public IActionResult Patch(int key, [FromBody] Delta<Book> movie)
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
            movie.Patch(entity);
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
        public IActionResult Put(int key, [FromBody] Book update)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (key != update.Id)
            {
                return BadRequest();
            }
            _db.Entry(update).State = EntityState.Modified;
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
            var movie = _db.Books.Find(key);
            if (movie == null)
            {
                return NotFound();
            }
            _db.Books.Remove(movie);
            _db.SaveChanges();
            return StatusCode((int)System.Net.HttpStatusCode.NoContent);
        }

        private bool _booksExists(int key)
        {
            return _db.Books.Any(x => x.Id == key);
        }
    }
}
