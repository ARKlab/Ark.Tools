using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODataEntityFrameworkSample.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ODataEntityFrameworkSample.Controllers
{
	[ApiVersion("1.0")]
    [Route("Tests")]
    public class TestsController : ControllerBase
    {
        private ODataSampleContext _db;

        public TestsController(ODataSampleContext context)
        {
            _db = context;
        }

        [HttpGet]
		public async Task<IActionResult> Get(ODataQueryOptions<Test> options)
        {
			
			var q2 = options.ApplyTo(_db.Tests) as IQueryable<Test>;
			var set = await q2.ToListAsync();
			return Ok(set);
		}


    }
}
