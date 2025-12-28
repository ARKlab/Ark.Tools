using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.Solid;

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
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BookPrintProcessController : ControllerBase
    {
        private readonly IRequestHandler<BookPrintProcess_CreateRequest.V1, BookPrintProcess.V1.Output> _createHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="BookPrintProcessController"/> class
        /// </summary>
        public BookPrintProcessController(
            IRequestHandler<BookPrintProcess_CreateRequest.V1, BookPrintProcess.V1.Output> createHandler)
        {
            ArgumentNullException.ThrowIfNull(createHandler);

            _createHandler = createHandler;
        }

        /// <summary>
        /// Create a new book print process
        /// </summary>
        /// <param name="request">The print process creation data</param>
        /// <param name="ctk">Cancellation token</param>
        /// <returns>The created print process</returns>
        [HttpPost]
        [ProducesResponseType(typeof(BookPrintProcess.V1.Output), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post(
            [FromBody] BookPrintProcess_CreateRequest.V1 request,
            CancellationToken ctk = default)
        {
            var result = await _createHandler.ExecuteAsync(request, ctk).ConfigureAwait(false);

            return CreatedAtAction(nameof(Post), new { id = result.BookPrintProcessId }, result);
        }
    }
}
