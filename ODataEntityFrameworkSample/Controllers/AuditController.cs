using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Controllers
{
	[ApiVersion("1.0")]
    [ODataRoutePrefix("Audits")]
    public class AuditController : ODataController
    {
        private ODataSampleContext _db;

        public AuditController(ODataSampleContext context)
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
