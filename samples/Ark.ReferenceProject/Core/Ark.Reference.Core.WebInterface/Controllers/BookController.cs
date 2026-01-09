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

namespace Ark.Reference.Core.WebInterface.Controllers;

/// <summary>
/// Controller for managing Book entities
/// </summary>
[Route("book")]
public class BookController : ApiController
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRequestProcessor _requestProcessor;

    public BookController(
        IQueryProcessor queryProcessor,
        IRequestProcessor requestProcessor
        )
    {
        _queryProcessor = queryProcessor;
        _requestProcessor = requestProcessor;
    }

    /// <summary>
    /// Create a new Book
    /// </summary>
    /// <param name="create">Payload for Create</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>The created Book entity</returns>
    /// <response code="200">Success</response>
    [HttpPost]
    [ProducesResponseType(typeof(Book.V1.Output), 200)]
    public async Task<IActionResult> CreateBook(
        [FromBody] Book.V1.Create create,
        CancellationToken ctk = default)
    {
        var res = await _requestProcessor.ExecuteAsync(new Book_CreateRequest.V1()
        {
            Data = create
        }, ctk).ConfigureAwait(false);

        return Ok(res);
    }

    /// <summary>
    /// Get an existing Book by Id
    /// </summary>
    /// <param name="id">The Id of the Book</param>
    /// <param name="ctk">Cancellation Token</param>
    /// <returns>The Book entity</returns>
    /// <response code="404">Book with supplied Id does not exist</response>
    /// <response code="200">Success</response>
    [HttpGet]
    [Route("{id}")]
    [ProducesResponseType(typeof(Book.V1.Output), 200)]
    public async Task<IActionResult> GetById(
        [FromRoute] int id,
        CancellationToken ctk = default)
    {
        var res = await _queryProcessor.ExecuteAsync(new Book_GetByIdQuery.V1()
        {
            Id = id
        }, ctk).ConfigureAwait(false);

        if (res == null)
            throw new EntityNotFoundException($"Book with id '{id}' not found");

        return Ok(res);
    }

    /// <summary>
    /// Get Books by filters
    /// </summary>
    /// <param name="id">The Id of the Book</param>
    /// <param name="title">The Title of the Book</param>
    /// <param name="author">The Author of the Book</param>
    /// <param name="genre">The Genre of the Book</param>
    /// <param name="sort">Sort criteria</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="ctk">Cancellation Token</param>
    /// <returns>Paged result of Book entities</returns>
    /// <response code="200">Success</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<Book.V1.Output>), 200)]
    public async Task<IActionResult> GetByFilters(
        [FromQuery] int[] id,
        [FromQuery] string[] title,
        [FromQuery] string[] author,
        [FromQuery] BookGenre[] genre,
        [FromQuery] string[] sort,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 10,
        CancellationToken ctk = default)
    {
        var res = await _queryProcessor.ExecuteAsync(new Book_GetByFiltersQuery.V1()
        {
            Id = id,
            Title = title,
            Author = author,
            Genre = genre,

            Sort = sort,
            Limit = limit,
            Skip = skip,
        }, ctk).ConfigureAwait(false);

        return Ok(res);
    }

    /// <summary>
    /// Update a Book by Id
    /// </summary>
    /// <param name="id">Id of the Book to update</param>
    /// <param name="update">The input information to update the Book</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>The updated Book entity</returns>
    /// <response code="200">Success</response>
    /// <response code="404">Book with supplied Id does not exist</response>
    [HttpPut]
    [Route("{id}")]
    [ProducesResponseType(typeof(Book.V1.Output), 200)]
    public async Task<IActionResult> UpdateBook(
        [FromRoute] int id,
        [FromBody] Book.V1.Update update,
        CancellationToken ctk = default
    )
    {
        if (update.Id == 0)
        {
            update = update with { Id = id };
        }

        var res = await _requestProcessor.ExecuteAsync(new Book_UpdateRequest.V1()
        {
            Id = id,
            Data = update
        }, ctk).ConfigureAwait(false);

        if (res == null)
            throw new EntityNotFoundException($"Book with id '{id}' not found");

        return Ok(res);
    }

    /// <summary>
    /// Deletes a Book
    /// </summary>
    /// <param name="id">Id of entity</param>
    /// <param name="ctk">Cancellation Token</param>
    /// <returns>True if deleted successfully</returns>
    /// <response code="200">Success</response>
    /// <response code="404">Book with supplied Id does not exist</response>
    [HttpDelete]
    [Route("{id}")]
    [ProducesResponseType(typeof(bool), 200)]
    public async Task<IActionResult> DeleteBook(
        [FromRoute] int id,
        CancellationToken ctk = default)
    {
        var res = await _requestProcessor.ExecuteAsync(new Book_DeleteRequest.V1()
        {
            Id = id
        }, ctk).ConfigureAwait(false);

        if (res)
            return Ok(res);

        throw new EntityNotFoundException($"Book with id '{id}' not found");
    }
}