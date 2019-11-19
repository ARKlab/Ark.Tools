using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.Core;
using Ark.Tools.Solid;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NodaTime;
using SimpleWebApplication.Api.Queries;
using SimpleWebApplication.Dto;

namespace SimpleWebApplication.Controllers
{
    //[ApiVersion("1.0")]
    [Route("entity")]
    [ApiController]
    public class EntityController : ApiController
	{
        //private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IQueryProcessor _queryProcessor;
        private readonly IRequestProcessor _requestProcessor;

        public EntityController(IQueryProcessor queryProcessor, IRequestProcessor requestProcessor)
        {
            _queryProcessor = queryProcessor;
            _requestProcessor = requestProcessor;
        }

        /// <summary>
        /// Get a Entity by Id - Try with text: 'null' for a null entity - 'ensure' for ensure error
        /// </summary>
        /// <param name="entityId">The Entity identifier</param>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet(@"{entityId}")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Get_Entity([FromRoute] string entityId, CancellationToken ctk = default)
        {
            var query = new Get_EntityByIdQuery.V1()
            {
                EntityId = entityId
            };

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            if (res == null)
                return this.NotFound();

            return this.Ok(res);
        }

    }
}