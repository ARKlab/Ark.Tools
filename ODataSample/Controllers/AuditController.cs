using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;

namespace ODataSample.Controllers
{
	[ApiVersion("1.0")]
    [ODataRoutePrefix("Audits")]
    public class AuditController : ODataController
    {
        private BookStoreContext _db;

        public AuditController(BookStoreContext context)
        {
            _db = context;
        }

        [HttpGet]
        [ODataRoute]
        [EnableQuery]
        public IActionResult Get()
        {
            return Ok(_db.Audits);
        }

        [HttpGet("({key})")]
        [ODataRoute("({key})")]
        [EnableQuery]
        public IActionResult Get([FromODataUri]int key)
        {
            return Ok(_db.Audits.Find(key));
        }

    }
}
