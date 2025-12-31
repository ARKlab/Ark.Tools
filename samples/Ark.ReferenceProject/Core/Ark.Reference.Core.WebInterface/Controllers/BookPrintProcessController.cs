using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;

using Asp.Versioning;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.WebInterface.Controllers
{
    /// <summary>
    /// Controller for managing book print processes
    /// </summary>
    [ApiVersion("1.0")]
    [Route("bookPrintProcess")]
    [ApiController]
    public class BookPrintProcessController : ControllerBase
    {
        private readonly IQueryProcessor _queryProcessor;
        private readonly IRequestHandler<BookPrintProcess_CreateRequest.V1, BookPrintProcess.V1.Output> _createHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="BookPrintProcessController"/> class
        /// </summary>
        public BookPrintProcessController(
            IQueryProcessor queryProcessor,
            IRequestHandler<BookPrintProcess_CreateRequest.V1, BookPrintProcess.V1.Output> createHandler)
        {
            ArgumentNullException.ThrowIfNull(queryProcessor);
            ArgumentNullException.ThrowIfNull(createHandler);

            _queryProcessor = queryProcessor;
            _createHandler = createHandler;
        }

        /// <summary>
        /// Get an existing BookPrintProcess by Id
        /// </summary>
        /// <param name="id">The Id of the BookPrintProcess</param>
        /// <param name="ctk">Cancellation Token</param>
        /// <returns>The BookPrintProcess entity</returns>
        /// <response code="404">BookPrintProcess with supplied Id does not exist</response>
        /// <response code="200">Success</response>
        [HttpGet]
        [Route("{id}")]
        [ProducesResponseType(typeof(BookPrintProcess.V1.Output), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(
            [FromRoute] int id,
            CancellationToken ctk = default)
        {
            var res = await _queryProcessor.ExecuteAsync(new BookPrintProcess_GetByIdQuery.V1()
            {
                BookPrintProcessId = id
            }, ctk).ConfigureAwait(false);

            if (res == null)
                throw new EntityNotFoundException($"BookPrintProcess with id '{id}' not found");

            return Ok(res);
        }

        /// <summary>
        /// Get BookPrintProcesses by filters
        /// </summary>
        /// <param name="bookPrintProcessId">The Id of the BookPrintProcess</param>
        /// <param name="bookId">The BookId to filter by</param>
        /// <param name="status">The Status to filter by</param>
        /// <param name="sort">Sort criteria</param>
        /// <param name="skip">Number of items to skip</param>
        /// <param name="limit">Maximum number of items to return</param>
        /// <param name="ctk">Cancellation Token</param>
        /// <returns>Paged result of BookPrintProcess entities</returns>
        /// <response code="200">Success</response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<BookPrintProcess.V1.Output>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByFilters(
            [FromQuery] int[] bookPrintProcessId,
            [FromQuery] int[] bookId,
            [FromQuery] BookPrintProcessStatus[] status,
            [FromQuery] string[] sort,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 10,
            CancellationToken ctk = default)
        {
            var res = await _queryProcessor.ExecuteAsync(new BookPrintProcess_GetByFiltersQuery.V1()
            {
                BookPrintProcessId = bookPrintProcessId,
                BookId = bookId,
                Status = status,
                Sort = sort,
                Limit = limit,
                Skip = skip,
            }, ctk).ConfigureAwait(false);

            return Ok(res);
        }

        /// <summary>
        /// Create a new book print process
        /// </summary>
        /// <param name="data">The print process creation data</param>
        /// <param name="ctk">Cancellation token</param>
        /// <returns>The created print process</returns>
        [HttpPost]
        [ProducesResponseType(typeof(BookPrintProcess.V1.Output), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post(
            [FromBody] BookPrintProcess.V1.Create data,
            CancellationToken ctk = default)
        {
            var result = await _createHandler.ExecuteAsync(new BookPrintProcess_CreateRequest.V1 { Data = data }, ctk).ConfigureAwait(false);

            return CreatedAtAction(nameof(Post), new { id = result.BookPrintProcessId }, result);
        }
    }
}
