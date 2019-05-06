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
	[ODataRoutePrefix("Universities")]
	public class UniversitiesController : ODataController
	{
		private ODataSampleContext _db;

		public UniversitiesController(ODataSampleContext context)
		{
			_db = context;
		}

		//[HttpGet]
		[ODataRoute]
		//[EnableQuery]
		[Produces("application/json")]
		[ProducesResponseType(typeof(ODataValue<IEnumerable<University>>), StatusCodes.Status200OK)]
		[EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select)]
		public IActionResult Get()
		{
			return Ok(_db.Universities.Include(x => x.People));
		}

		[HttpGet("({key})")]
		[ODataRoute("({key})")]
		[EnableQuery]
		public IActionResult Get(int key)     //[FromODataUri] ????
		{
			if (key == 0)
			{
				var asOf = SystemClock.Instance.GetCurrentInstant();

				var UniversityList = _db
				  .Universities
				  .SqlServerAsOf(asOf)
				  .ToList();

				return Ok(UniversityList);
			}

			return Ok(_db.Universities.Include(x => x.People).Where(x => x.Id == key));
		}

		[HttpPost]
		[ODataRoute]
		[EnableQuery]
		public IActionResult Post([FromBody]University University)
		{
			_db.Universities.Add(University);
			_db.SaveChanges();
			return Created(University);
		}

		[HttpPatch("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Patch(int key, [FromBody] Delta<University> University)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			var entity = _db.Universities.Find(key);
			if (entity == null)
			{
				return NotFound();
			}
			University.Patch(entity);
			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_UniversitiesExists(key))
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
		public IActionResult Put_Universities(int key, [FromBody] University update)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			if (key != update.Id)
			{
				return BadRequest();
			}

			var university = _db.Universities.AsTracking()
				.Include(x => x.People).SingleOrDefault(x => x.Id == key);

			 //_db.Universities.AsTracking()
				//.Include(x => x.Registry).SingleOrDefault(x => x.Id == key);

			_db.Universities.Persist().InsertOrUpdate(update);

			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_UniversitiesExists(key))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return Updated(university);
		}


		[HttpDelete("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Delete(int key)
		{
			var University = _db.Universities.Find(key);
			if (University == null)
			{
				return NotFound();
			}
			_db.Universities.Remove(University);
			_db.SaveChanges();
			return StatusCode((int)System.Net.HttpStatusCode.NoContent);
		}

		private bool _UniversitiesExists(int key)
		{
			return _db.Universities.Any(x => x.Id == key);
		}
	}
}
