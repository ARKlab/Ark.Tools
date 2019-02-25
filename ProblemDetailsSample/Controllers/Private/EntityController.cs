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
        /// Get a Entity by Id
        /// - Try with text 'null' for a null entity
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
        /// Returns a BadRequest with Payload
        /// Try with text 'null' for returning a null entity
        /// </summary>
        /// <param name="ctk"></param>
        /// <returns></returns>
        [HttpGet(@"BadRequestPayload")]
        [ProducesResponseType(typeof(Entity.V1.Output), 200)]
        public async Task<IActionResult> Get_EntityBadRequestPayload(CancellationToken ctk = default)
        {

            var error = new Error()
            {
                Message = "error"
            };

            return await Task.FromResult(BadRequest(error));
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
            var query = new Get_EntityByIdExceptionQuery.V1()
            {
                EntityId = "Test"
            };

            var res = await _queryProcessor.ExecuteAsync(query, ctk);

            return this.Ok(res);
        }
    }

    internal class Error
    {
        public string Message { get; set; }
    }
}