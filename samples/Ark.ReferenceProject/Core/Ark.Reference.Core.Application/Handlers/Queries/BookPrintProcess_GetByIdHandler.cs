using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

/// <summary>
/// Handler for retrieving a BookPrintProcess entity by ID
/// </summary>
public class BookPrintProcess_GetByIdHandler : IQueryHandler<BookPrintProcess_GetByIdQuery.V1, BookPrintProcess.V1.Output?>
{
    private readonly ICoreDataContextFactory _coreDataContext;

    public BookPrintProcess_GetByIdHandler(ICoreDataContextFactory coreDataContext)
    {
        _coreDataContext = coreDataContext;
    }

    /// <inheritdoc/>
    public BookPrintProcess.V1.Output? Execute(BookPrintProcess_GetByIdQuery.V1 query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(query).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <inheritdoc/>
    public async Task<BookPrintProcess.V1.Output?> ExecuteAsync(BookPrintProcess_GetByIdQuery.V1 query, CancellationToken ctk = default)
    {
        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);
        return await ctx.ReadBookPrintProcessByIdAsync(query.BookPrintProcessId, ctk).ConfigureAwait(false);
    }
}
