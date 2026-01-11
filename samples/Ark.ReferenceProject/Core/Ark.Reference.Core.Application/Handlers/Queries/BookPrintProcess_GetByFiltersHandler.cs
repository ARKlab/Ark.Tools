using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

/// <summary>
/// Handler for retrieving BookPrintProcess entities by filters
/// </summary>
public class BookPrintProcess_GetByFiltersHandler : IQueryHandler<BookPrintProcess_GetByFiltersQuery.V1, PagedResult<BookPrintProcess.V1.Output>>
{
    private readonly ICoreDataContextFactory _coreDataContext;

    public BookPrintProcess_GetByFiltersHandler(ICoreDataContextFactory coreDataContext)
    {
        _coreDataContext = coreDataContext;
    }

    /// <inheritdoc/>
    public PagedResult<BookPrintProcess.V1.Output> Execute(BookPrintProcess_GetByFiltersQuery.V1 query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(query).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <inheritdoc/>
    public async Task<PagedResult<BookPrintProcess.V1.Output>> ExecuteAsync(BookPrintProcess_GetByFiltersQuery.V1 query, CancellationToken ctk = default)
    {
        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        var searchQuery = new BookPrintProcessSearchQueryDto.V1
        {
            BookPrintProcessId = query.BookPrintProcessId ?? [],
            BookId = query.BookId ?? [],
            Status = query.Status ?? [],
            Sort = query.Sort ?? [],
            Skip = query.Skip,
            Limit = query.Limit
        };

        var (data, count) = await ctx.ReadBookPrintProcessByFiltersAsync(searchQuery, ctk).ConfigureAwait(false);

        return new PagedResult<BookPrintProcess.V1.Output>
        {
            Count = count,
            Data = data.ToList(),
            Limit = query.Limit,
            Skip = query.Skip,
            IsCountPartial = false
        };
    }
}
