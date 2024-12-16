using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.Core;
using Ark.Tools.Solid;

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using WebApplicationDemo.Api.Queries;
using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Controllers.V1
{
    [ApiVersion("1.0")]
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
        /// <param name="result">The Entity Result </param>
        /// <param name="tests">The Entity test array </param>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet(@"{entityId}")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        [Produces("application/json", "application/problem+json", "application/x-msgpack")]
        [Consumes("application/json", "application/x-msgpack")]
        public async Task<IActionResult> Get_Entity([FromRoute] string? entityId, [FromQuery] EntityResult result, [FromQuery] EntityTest[] tests, CancellationToken ctk = default)
        {
            var query = new Get_EntityByIdQuery.V1()
            {
                EntityId = entityId,
            };

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            if (res == null)
                return NotFound();

            return Ok(res);
        }

        /// <summary>
        /// Returns a BusinessRuleViolation
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost(@"BusinessRuleViolation")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Post_BusinessRuleViolation([FromBody] Entity.V1.Input body)
        {
            var request = new Post_EntityRequestBusinessRuleViolation.V1()
            {
                EntityId = body.EntityId
            };

            var res = await _requestProcessor.ExecuteAsync(request, default);

            return Ok(res);
        }


        /// <summary>
        /// Returns a BusinessRuleViolation
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost(@"ArkProblemDetails")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Post_ArkProblemDetails([FromBody] Entity.V1.Input body)
        {
            var request = new Post_EntityRequestProblemDetails.V1()
            {
                EntityId = body.EntityId
            };

            var res = await _requestProcessor.ExecuteAsync(request, default);

            return Ok(res);
        }

        /// <summary>
        /// Returns Handler EntityNotFoundException
        /// </summary>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet(@"EntityNotFound")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public Task<IActionResult> Get_EntityHandlerNotFound(CancellationToken ctk = default)
        {
            throw new EntityNotFoundException("Entyty not here.");

        }

        /// <summary>
        /// Returns Generic Exception
        /// </summary>
        /// <returns></returns>
        [HttpGet(@"GenericException")]
        public IActionResult Get_GenericException()
        {
            throw new OperationException("This is a Generic Exception thrown from an Web API controller.");
        }


        /// <summary>
        /// Returns a Validation Fails
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        [Produces("application/json", "application/problem+json", "application/x-msgpack")]
        [Consumes("application/json", "application/x-msgpack")]
        public async Task<IActionResult> Post([FromBody] Entity.V1.Input body)
        {
            var request = new Post_EntityRequest.V1(body)
            {
            };

            var res = await _requestProcessor.ExecuteAsync(request, default);

            return Ok(res);
        }

        /// <summary>
        /// Returns a Validation Fails
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost(@"FluentValidationFails")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Post_ValidationFails([FromBody] Entity.V1.Input body)
        {
            var request = new Post_EntityRequest.V1(body)
            {
                EntityId = "StringLongerThan10"
            };

            var res = await _requestProcessor.ExecuteAsync(request, default);

            return Ok(res);
        }
    }
}