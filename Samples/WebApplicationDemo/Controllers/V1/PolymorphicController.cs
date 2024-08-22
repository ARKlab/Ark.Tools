using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.Solid;

using Asp.Versioning;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using WebApplicationDemo.Api.Queries;
using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("polymorphycs")]
    [ApiController]
    public class PolymorphicController : ApiController
    {
        private readonly IQueryProcessor _queryProcessor;
        private readonly IRequestProcessor _requestProcessor;

        public PolymorphicController(IQueryProcessor queryProcessor, IRequestProcessor requestProcessor)
        {
            _queryProcessor = queryProcessor;
            _requestProcessor = requestProcessor;
        }

        /// <summary>
        /// Get a Entity by Id - Try with text: 'null' for a null entity - 'ensure' for ensure error
        /// </summary>
        /// <param name="id">The identifier</param>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Polymorphic), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromRoute] string id, CancellationToken ctk = default)
        {
            var query = new Get_EntityByIdQuery.V1()
            {
                EntityId = id,
            };

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            if (res == null)
                return NotFound();

            return Ok(res);
        }


        [HttpPost]
        [ProducesResponseType(typeof(Polymorphic), StatusCodes.Status200OK)]
        public async Task<IActionResult> Post([FromBody] Polymorphic entity, CancellationToken ctk = default)
        {
            var request = new Post_PolymorphicRequest.V1()
            {
                Entity = entity,
            };

            var res = await _requestProcessor.ExecuteAsync(request, ctk);

            return Ok(res);
        }
    }
}
