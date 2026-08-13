using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.Reference.Core.Application.Handlers.Requests;

/// <summary>
/// Handles bulk creation of books.
/// </summary>
public sealed class Book_BulkCreateRequestHandler : IRequestHandler<Book_BulkCreateRequest.V1, IEnumerable<Book.V1.Output>>
{
    private readonly ICoreDataContextFactory _coreDataContext;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Book_BulkCreateRequestHandler"/> class.
    /// </summary>
    /// <param name="coreDataContext">The data context factory.</param>
    /// <param name="userContext">The current user context.</param>
    public Book_BulkCreateRequestHandler(
        ICoreDataContextFactory coreDataContext,
        IContextProvider<ClaimsPrincipal> userContext)
    {
        ArgumentNullException.ThrowIfNull(coreDataContext);
        ArgumentNullException.ThrowIfNull(userContext);

        _coreDataContext = coreDataContext;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Book.V1.Output>> ExecuteAsync(
        Book_BulkCreateRequest.V1 request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request.Data);

        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        await ctx.EnsureAudit(AuditKind.Book, _userContext.GetUserId(), "Bulk create Books", ctk).ConfigureAwait(false);

        var books = request.Data
            .Select(static data => new Book.V1.Output
            {
                Title = data.Title,
                Author = data.Author,
                Genre = data.Genre,
                ISBN = data.ISBN,
                Description = $"Book created: {data.Title} by {data.Author}"
            })
            .ToArray();

        var created = await ctx.BulkInsertBooksAsync(books, ctk).ConfigureAwait(false);
        await ctx.CommitAsync(ctk).ConfigureAwait(false);

        return created;
    }
}
