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
	[ODataRoutePrefix("Schools")]
	public class SchoolsController : ODataController
	{
		private ODataSampleContext _db;

		public SchoolsController(ODataSampleContext context)
		{
			_db = context;
		}

		//[HttpGet]
		[ODataRoute]
		//[EnableQuery]
		[Produces("application/json")]
		[ProducesResponseType(typeof(ODataValue<IEnumerable<School>>), StatusCodes.Status200OK)]
		[EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select)]
		public IActionResult Get()
		{
			return Ok(_db.Schools.Include(x => x.Students));
		}

		[HttpGet("({key})")]
		[ODataRoute("({key})")]
		[EnableQuery]
		public IActionResult Get(int key)     //[FromODataUri] ????
		{
			if (key == 0)
			{
				var asOf = SystemClock.Instance.GetCurrentInstant();

				var SchoolList = _db
				  .Schools
				  .SqlServerAsOf(asOf)
				  .ToList();

				return Ok(SchoolList);
			}

			return Ok(_db.Schools.Include(x => x.Students).Where(x => x.Id == key));
		}

		[HttpPost]
		[ODataRoute]
		[EnableQuery]
		public IActionResult Post([FromBody]School School)
		{
			_db.Schools.Add(School);
			_db.SaveChanges();
			return Created(School);
		}

		[HttpPatch("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Patch(int key, [FromBody] Delta<School> School)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			var entity = _db.Schools.Find(key);
			if (entity == null)
			{
				return NotFound();
			}
			School.Patch(entity);
			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_SchoolsExists(key))
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
		public IActionResult Put_Schools(int key, [FromBody] SchoolDto update)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			if (key != update.Id)
			{
				return BadRequest();
			}

			var school = _db.Schools.AsTracking()
				.Include(x => x.Students).SingleOrDefault(x => x.Id == key);

			 //_db.Schools.AsTracking()
				//.Include(x => x.Registry).SingleOrDefault(x => x.Id == key);

			_db.Schools.Persist().InsertOrUpdate(update);

			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_SchoolsExists(key))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return Updated(school);
		}


		[HttpDelete("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Delete(int key)
		{
			var School = _db.Schools.Find(key);
			if (School == null)
			{
				return NotFound();
			}
			_db.Schools.Remove(School);
			_db.SaveChanges();
			return StatusCode((int)System.Net.HttpStatusCode.NoContent);
		}

		private bool _SchoolsExists(int key)
		{
			return _db.Schools.Any(x => x.Id == key);
		}
	}
}
