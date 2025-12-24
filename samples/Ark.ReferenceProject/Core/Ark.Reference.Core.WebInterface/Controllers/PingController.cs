using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.WebInterface.Utils;
using Ark.Tools.Core;
using Ark.Tools.Solid;

using Microsoft.AspNetCore.Mvc;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.WebInterface.Controllers
{
    [Route("ping")]
    public class PingController : ApiController
    {
        private readonly IQueryProcessor _queryProcessor;
        private readonly IRequestProcessor _requestProcessor;

        public PingController(
            IQueryProcessor queryProcessor,
            IRequestProcessor requestProcessor
            )
        {
            _queryProcessor = queryProcessor;
            _requestProcessor = requestProcessor;
        }

        /// <summary>
        /// Returns pong.
        /// </summary>
        /// <remarks>Can be called anonymously</remarks>
        /// <response code="200">Success</response>
        [HttpGet]
        [Route("test")]
        public IActionResult Get(CancellationToken ctk = default)
        {
            return this.Ok("pong");
        }

        /// <summary>
        /// Return a concatenated string starting from the provided name
        /// Test the flow till the handler 
        /// </summary>
        /// <param name="name">Name to concatenate</param>
        /// <param name="ctk">Cancellation Token</param>
        /// <returns></returns>
        [HttpGet]
        [Route("test/{name}")]
        public async Task<IActionResult> TestName(
            [FromRoute] string name,
            CancellationToken ctk = default)
        {
            var res = await _queryProcessor.ExecuteAsync(new Ping_GetByNameQuery.V1()
            {
                Name = name
            }, ctk).ConfigureAwait(false);

            return this.Ok(res);
        }

        /// <summary>
        /// Create a new Ping
        /// </summary>
        /// <param name="create">Payload for Create</param>
        /// <param name="ctk">Cancellation token</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(typeof(Ping.V1.Output), 200)]
        public async Task<IActionResult> Create_Ping(
            [FromBody] Ping.V1.Create create,
            CancellationToken ctk = default)
        {
            var res = await _requestProcessor.ExecuteAsync(new Ping_CreateRequest.V1()
            {
                Data = create
            }, ctk).ConfigureAwait(false);

            return Ok(res);
        }

        /// <summary>
        /// Get an existing ping by Id
        /// </summary>
        /// <param name="id">The Id of the Ping</param>
        /// <param name="ctk">Cancellation Token</param>
        /// <returns></returns>
        /// <response code="404">Ping with supplied Id does not exist</response>
        /// <response code="200">Success</response>
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetById(
            [FromRoute] int id,
            CancellationToken ctk = default)
        {
            var res = await _queryProcessor.ExecuteAsync(new Ping_GetByIdQuery.V1()
            {
                Id = id
            }, ctk).ConfigureAwait(false);

            if (res == null)
                throw new EntityNotFoundException($"Ping with id '{id}' not found");

            return Ok(res);
        }

        /// <summary>
        /// Get pings by filters
        /// </summary>
        /// <param name="id">The Id of the Ping</param>
        /// <param name="name">The Name of the Ping</param>
        /// <param name="type">The Type of the Ping</param>
        /// <param name="sort">sort</param>
        /// <param name="skip">skip</param>
        /// <param name="limit">limit</param>
        /// <param name="ctk">Cancellation Token</param>
        /// <returns></returns>
        /// <response code="200">Success</response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Ping.V1.Output>), 200)]
        public async Task<IActionResult> GetByFilters(
            [FromQuery] int[] id,
            [FromQuery] string[] name,
            [FromQuery] PingType[] type,
            [FromQuery] string[] sort,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 10,
            CancellationToken ctk = default)
        {
            var res = await _queryProcessor.ExecuteAsync(new Ping_GetByFiltersQuery.V1()
            {
                Id = id,
                Name = name,
                Type = type,

                Sort = sort,
                Limit = limit,
                Skip = skip,
            }, ctk).ConfigureAwait(false);

            return Ok(res);
        }

        /// <summary>
        /// Update a Ping by Id
        /// </summary>
        /// <param name="id">Id of the Ping to update</param>
        /// <param name="update">The input information to upsert the ping</param>
        /// <param name="ctk">Cancellation token</param>
        /// <returns></returns>
        /// <response code="200">Success</response>
        /// <response code="404">Ping with supplied Id does not exist</response>
        [HttpPut]
        [Route("{id}")]
        public async Task<IActionResult> Put_Ping(
            [FromRoute] int id,
            [FromBody] Ping.V1.Update update,
            CancellationToken ctk = default
        )
        {
            if (update.Id == 0)
            {
                update = update with { Id = id };
            }

            var res = await _requestProcessor.ExecuteAsync(new Ping_UpdatePutRequest.V1()
            {
                Id = id,
                Data = update
            }, ctk).ConfigureAwait(false);

            if (res == null)
                throw new EntityNotFoundException($"Ping with id '{id}' not found");

            return Ok(res);
        }

        /// <summary>
        /// Partially update a Ping by Id
        /// </summary>
        /// <param name="id">Id of the Ping to update</param>
        /// <param name="update">The partial update information for the ping</param>
        /// <param name="ctk">Cancellation token</param>
        /// <returns>The updated Ping entity</returns>
        /// <response code="200">Success</response>
        /// <response code="404">Ping with supplied Id does not exist</response>
        [HttpPatch]
        [Route("{id}")]
        public async Task<IActionResult> Patch_Ping(
            [FromRoute] int id,
            [FromBody] Ping.V1.Update update,
            CancellationToken ctk = default
        )
        {
            if (update.Id == 0)
            {
                update = update with { Id = id };
            }

            var res = await _requestProcessor.ExecuteAsync(new Ping_UpdatePatchRequest.V1()
            {
                Id = id,
                Data = update
            }, ctk).ConfigureAwait(false);

            if (res == null)
                throw new EntityNotFoundException($"Ping with id '{id}' not found");

            return Ok(res);
        }

        /// <summary>
        /// Deletes a Ping
        /// </summary>
        /// <param name="id">Id of entity</param>
        /// <param name="ctk">Cancellation Token</param>
        /// <returns></returns>
        /// <response code="200">Success</response>
        [HttpDelete]
        [Route("{id}")]
        [ProducesResponseType(typeof(bool), 200)]
        public async Task<IActionResult> Delete_Ping(
            [FromRoute] int id,
            CancellationToken ctk = default)
        {
            var res = await _requestProcessor.ExecuteAsync(new Ping_DeleteRequest.V1()
            {
                Id = id
            }, ctk).ConfigureAwait(false);

            if (res)
                return Ok(res);

            throw new EntityNotFoundException($"Ping with id '{id}' not found");
        }

        /// <summary>
        /// Create a new Ping and Send Mesasge
        /// </summary>
        /// <param name="create">Payload for Create</param>
        /// <param name="ctk">Cancellation token</param>
        /// <returns></returns>
        [HttpPost("message")]
        [ProducesResponseType(typeof(Ping.V1.Output), 200)]
        public async Task<IActionResult> Create_PingAndSendMsg(
            [FromBody] Ping.V1.Create create,
            CancellationToken ctk = default)
        {
            var res = await _requestProcessor.ExecuteAsync(new Ping_CreateAndSendMsgRequest.V1()
            {
                Data = create
            }, ctk).ConfigureAwait(false);

            return Ok(res);
        }
    }
}