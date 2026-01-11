using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.Reference.Core.Application.Handlers.Requests;

/// <summary>
/// Handler for creating a new Book entity
/// </summary>
public class Book_CreateRequestHandler : IRequestHandler<Book_CreateRequest.V1, Book.V1.Output>
{
    private readonly ICoreDataContextFactory _coreDataContext;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    public Book_CreateRequestHandler(
        ICoreDataContextFactory coreDataContext,
        IContextProvider<ClaimsPrincipal> userContext)
    {
        ArgumentNullException.ThrowIfNull(coreDataContext);
        ArgumentNullException.ThrowIfNull(userContext);

        _coreDataContext = coreDataContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public Book.V1.Output Execute(Book_CreateRequest.V1 request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(request).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <inheritdoc/>
    public async Task<Book.V1.Output> ExecuteAsync(Book_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request.Data);

        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        await ctx.EnsureAudit(AuditKind.Book, _userContext.GetUserId(), "Create a new Book", ctk).ConfigureAwait(false);

        var createBookData = new Book.V1.Output
        {
            Title = request.Data.Title,
            Author = request.Data.Author,
            Genre = request.Data.Genre,
            ISBN = request.Data.ISBN,
            Description = $"Book created: {request.Data.Title} by {request.Data.Author}"
        };

        var id = await ctx.InsertBookAsync(createBookData, ctk).ConfigureAwait(false);

        var entity = await ctx.ReadBookByIdAsync(id, ctk).ConfigureAwait(false);

        await ctx.CommitAsync(ctk).ConfigureAwait(false);

        return entity!;
    }
}
