using System.Linq;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using NodaTime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODataSample.Models;
using Ark.Tools.EntityFrameworkCore.SystemVersioning;

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

        [HttpGet]
        [ODataRoute]
        [EnableQuery]
        public IActionResult Get()
        {
            return Ok(_db.Books);
        }

        [HttpGet("({key})")]
        [ODataRoute("({key})")]
        [EnableQuery]
        public IActionResult Get(int key)     //[FromODataUri] ????
        {
			var asOf = SystemClock.Instance.GetCurrentInstant();

			if (key == -2)
			{
				var bookList = _db.Books.FromSql($@"
SELECT b.[Id]     
      ,b.[ISBN]
      ,b.[Title]
      ,b.[Author]
      ,b.[Price]
      ,b.[Location_City]
      ,b.[Location_Street]
      ,b.[AuditId]
      ,b.[PressId]
      ,b.[_ETag]
      ,b.[SysStartTime]
      ,b.[SysEndTime]
FROM		 dbo.[Audits] FOR SYSTEM_TIME AS OF {asOf} aud
INNER JOIN   dbo.[Books]  FOR SYSTEM_TIME AS OF {asOf} b ON b.AuditId = aud.AuditId").ToList();

				return Ok(bookList);
			}
			else if (key == 0)
			{
				var bookList = _db
				  .Books
				  .SqlServerBetween(asOf, asOf.PlusNanoseconds(10000))
				  .ToList();

				return Ok(bookList);
			}
			else if (key == -1)
			{
				var bookList = _db
				  .Books
				  .SqlServerAsOf(asOf)
				  .ToList();

				return Ok(bookList);
			}
			else
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
