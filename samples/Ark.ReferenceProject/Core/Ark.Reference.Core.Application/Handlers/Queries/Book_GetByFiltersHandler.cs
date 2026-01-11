using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

/// <summary>
/// Handler for retrieving Book entities by filters
/// </summary>
public class Book_GetByFiltersHandler : IQueryHandler<Book_GetByFiltersQuery.V1, PagedResult<Book.V1.Output>>
{
    private readonly ICoreDataContextFactory _coreDataContext;

    public Book_GetByFiltersHandler(ICoreDataContextFactory coreDataContext)
    {
        _coreDataContext = coreDataContext;
    }

    /// <inheritdoc/>
    public PagedResult<Book.V1.Output> Execute(Book_GetByFiltersQuery.V1 query)
    {
        return ExecuteAsync(query).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<Book.V1.Output>> ExecuteAsync(Book_GetByFiltersQuery.V1 query, CancellationToken ctk = default)
    {
        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        var searchQuery = new BookSearchQueryDto.V1
        {
            Id = query.Id ?? [],
            Title = query.Title ?? [],
            Author = query.Author ?? [],
            Genre = query.Genre ?? [],
            Sort = query.Sort ?? [],
            Skip = query.Skip,
            Limit = query.Limit
        };

        var (data, count) = await ctx.ReadBookByFiltersAsync(searchQuery, ctk).ConfigureAwait(false);

        return new PagedResult<Book.V1.Output>
        {
            Count = count,
            Data = data.ToList(),
            Limit = query.Limit,
            Skip = query.Skip,
            IsCountPartial = false
        };
    }
}