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
        private readonly IFlurlClientCache _flurl;
        private string _url;

        public FlurlDemoController(IFlurlClientCache flurl) 
        {
            _flurl = flurl;
            _url = "https://jsonplaceholder.typicode.com/";
        }

        [Route("posts")]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var client = _flurl.GetOrAdd(_url, _url);

            var response = await client.Request("posts").GetStringAsync();

            var data = JsonSerializer.Deserialize<List<Post>>(response);

            return Ok(data);    
        }
    }
}
