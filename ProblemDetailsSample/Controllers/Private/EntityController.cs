using System;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.AspNetCore.NestedStartup;
using Ark.Tools.Solid;
using NLog;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProblemDetailsSample.Api.Queries;
using ProblemDetailsSample.Common.Dto;
using ProblemDetailsSample.Models;
using ProblemDetailsSample.Api.Requests;

namespace ProblemDetailsSample.Controllers.Private
{
    [ApiVersion("1.0")]
    [Route("entity")]
    [ApiController]
    public class EntityController : Controller, IArea<PrivateArea>
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
        /// Get a Entity by Id - Try with text 'null' for a null entity
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

        /// <summary>
        /// Returns Handler EntityNotFoundException
        /// </summary>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet(@"EntityNotFound")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Get_EntityHandlerNotFound(CancellationToken ctk = default)
        {
            var query = new Get_EntityByIdNotFoundQuery.V1();

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            return this.Ok(res);
        }

        /// <summary>
        /// Returns Handler NotImplementedException
        /// </summary>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet(@"NotImplemented")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Get_EntityHandlerException(CancellationToken ctk = default)
        {
            var query = new Get_EntityByIdExceptionQuery.V1();

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            return this.Ok(res);
        }

        /// <summary>
        /// Returns a BadRequest with Payload
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost(@"PostEntityOK")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public IActionResult Post_EntityOK([FromBody]Entity.V1.Input body)
        {
            return this.Ok();
        }

        /// <summary>
        /// Returns a Validation Fails
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost(@"ValidationFails")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Post_ValidationFails([FromBody]Entity.V1.Input body)
        {
            var request = new Post_EntityRequest.V1()
            {
                EntityId = "StringLongerThan10"
            };

            var res = await _requestProcessor.ExecuteAsync(request, default);

            return this.Ok(res);
        }

        /// <summary>
        /// Returns a ArkProblemDetails
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost(@"ArkProblemDetails")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Post_ArkProblemDetails([FromBody]Entity.V1.Input body)
        {
            var request = new Post_EntityRequestProblemDetails.V1()
            {
                EntityId = body.EntityId
            };

            var res = await _requestProcessor.ExecuteAsync(request, default);

            return this.Ok(res);
        }
    }

    internal class Error
    {
        public string Message { get; set; }
    }
}