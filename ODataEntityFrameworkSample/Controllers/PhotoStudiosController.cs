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
using MoreLinq;
using AutoMapper.EntityFrameworkCore;

namespace ODataEntityFrameworkSample.Controllers
{
	[ApiVersion("1.0")]
	[ODataRoutePrefix("PhotoStudios")]
	public class PhotoStudiosController : ODataController
	{
		private ODataSampleContext _db;

		public PhotoStudiosController(ODataSampleContext context)
		{
			_db = context;
		}

		//[HttpGet]
		[ODataRoute]
		//[EnableQuery]
		[Produces("application/json")]
		[ProducesResponseType(typeof(ODataValue<IEnumerable<PhotoStudio>>), StatusCodes.Status200OK)]
		[EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select | AllowedQueryOptions.Expand)]
		public IActionResult Get()
		{
			return Ok(_db.PhotoStudios.Include(x => x.Workers));
		}

		[HttpGet("({key})")]
		[ODataRoute("({key})")]
		[EnableQuery]
		public IActionResult Get(int key)     //[FromODataUri] ????
		{
			if (key == 0)
			{
				var asOf = SystemClock.Instance.GetCurrentInstant();

				var PhotoStudioList = _db
				  .PhotoStudios
				  .SqlServerAsOf(asOf)
				  .ToList();

				return Ok(PhotoStudioList);
			}

			return Ok(_db.PhotoStudios.Include(x => x.Workers).Where(x => x.Id == key));
		}

		[HttpPost]
		[ODataRoute]
		[EnableQuery]
		public IActionResult Post([FromBody]PhotoStudio PhotoStudio)
		{
			_db.PhotoStudios.Add(PhotoStudio);
			_db.SaveChanges();
			return Created(PhotoStudio);
		}

		[HttpPatch("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Patch(int key, [FromBody] Delta<PhotoStudio> PhotoStudio)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			var entity = _db.PhotoStudios.Find(key);
			if (entity == null)
			{
				return NotFound();
			}
			PhotoStudio.Patch(entity);
			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_PhotoStudiosExists(key))
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
		public IActionResult Put_PhotoStudios(int key, [FromBody] PhotoStudio update)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			if (key != update.Id)
			{
				return BadRequest();
			}

			var PhotoStudio = _db.PhotoStudios.AsTracking()
				.Include(x => x.Workers).SingleOrDefault(x => x.Id == key);

			 //_db.PhotoStudios.AsTracking()
				//.Include(x => x.Registry).SingleOrDefault(x => x.Id == key);

			_db.PhotoStudios.Persist().InsertOrUpdate(update);

			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_PhotoStudiosExists(key))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return Updated(PhotoStudio);
		}


		[HttpDelete("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Delete(int key)
		{
			var PhotoStudio = _db.PhotoStudios.Find(key);
			if (PhotoStudio == null)
			{
				return NotFound();
			}
			_db.PhotoStudios.Remove(PhotoStudio);
			_db.SaveChanges();
			return StatusCode((int)System.Net.HttpStatusCode.NoContent);
		}

		private bool _PhotoStudiosExists(int key)
		{
			return _db.PhotoStudios.Any(x => x.Id == key);
		}
	}
}
