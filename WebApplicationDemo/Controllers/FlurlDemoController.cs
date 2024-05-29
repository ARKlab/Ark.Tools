using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.Core;
using Ark.Tools.Solid;

using Asp.Versioning;

using Flurl.Http;
using Flurl.Http.Configuration;
using Flurl.Http.Newtonsoft;

using Microsoft.AspNetCore.Mvc;

using WebApplicationDemo.Api.Queries;
using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

using System.Text.Json;
using System.Collections.Generic;

namespace WebApplicationDemo.Controllers
{
    [Route("flurl-demo")]
    [ApiVersion(3.0)]
    public class FlurlDemoController : ApiController
    {
        private readonly IQueryProcessor _queryProcessor;


        public FlurlDemoController(IQueryProcessor queryProcessor) 
        {
            _queryProcessor = queryProcessor;
        }

        [Route("posts")]
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ctk)
        {
            var query = new Get_PostsQuery.V1()
            {
            };

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            return Ok(res);
        }
    }
}
