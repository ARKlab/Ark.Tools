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

namespace ODataEntityFrameworkSample.Controllers
{
	[ApiVersion("1.0")]
	[ODataRoutePrefix("Countries")]
	public class CountriesController : ODataController
	{
		private ODataSampleContext _db;

		public CountriesController(ODataSampleContext context)
		{
			_db = context;
		}

		//[HttpGet]
		[ODataRoute]
		//[EnableQuery]
		[Produces("application/json")]
		[ProducesResponseType(typeof(ODataValue<IEnumerable<Country>>), StatusCodes.Status200OK)]
		[EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select)]
		public IActionResult Get()
		{
			return Ok(_db.Countries.Include(x => x.Cities));
		}

		[HttpGet("({key})")]
		[ODataRoute("({key})")]
		[EnableQuery]
		public IActionResult Get(int key)     //[FromODataUri] ????
		{
			if (key == 0)
			{
				var asOf = SystemClock.Instance.GetCurrentInstant();

				var countryList = _db
				  .Countries
				  .SqlServerAsOf(asOf)
				  .ToList();

				return Ok(countryList);
			}

			return Ok(_db.Countries.Include(x => x.Cities).Where(x => x.Id == key));
		}

		[HttpPost]
		[ODataRoute]
		[EnableQuery]
		public IActionResult Post([FromBody]Country country)
		{
			_db.Countries.Add(country);
			_db.SaveChanges();
			return Created(country);
		}

		[HttpPatch("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Patch(int key, [FromBody] Delta<Country> country)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			var entity = _db.Countries.Find(key);
			if (entity == null)
			{
				return NotFound();
			}
			country.Patch(entity);
			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_countriesExists(key))
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
		public IActionResult Put_Countries(int key, [FromBody] Country update)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			if (key != update.Id)
			{
				return BadRequest();
			}

			var country = _db.Countries.AsTracking()
				.Include(x => x.Cities).SingleOrDefault(x => x.Id == key);

			//OWNED_ENTITIES
			//To manage Add or Remove on OWNED collection entity we need to do this monster
			//Works but the record result as modyfied also if is not really added (are just same as before)
			//and the systart time is updated

			if (update.Cities != null)
			{
				foreach (var choice in update.Cities.FullJoin(country.Cities, u => u.Id,
					u => (toAdd: true, toRem: false, toUpdate: false, city: u, cur: null),
					c => (toAdd: false, toRem: true, toUpdate: false, city: c, cur: null),
					(u, c) => (toAdd: false, toRem: false, toUpdate: true, city: u, cur: c)
					))
				{
					if (choice.toAdd)
						country.Cities.Add(choice.city);
					if (choice.toRem)
						country.Cities.Remove(choice.city);
					if (choice.toUpdate)
					{
						_db.Entry(choice.cur).CurrentValues.SetValues(choice.city);
					}
				}
			}

			try
			{
				_db.SaveChanges();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!_countriesExists(key))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return Updated(country);
		}


		[HttpDelete("({key})")]
		[ODataRoute("({key})")]
		//[EnableQuery]
		public IActionResult Delete(int key)
		{
			var country = _db.Countries.Find(key);
			if (country == null)
			{
				return NotFound();
			}
			_db.Countries.Remove(country);
			_db.SaveChanges();
			return StatusCode((int)System.Net.HttpStatusCode.NoContent);
		}

		private bool _countriesExists(int key)
		{
			return _db.Countries.Any(x => x.Id == key);
		}
	}
}
