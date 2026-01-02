using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Queries
{
    /// <summary>
    /// Handler for retrieving a Book entity by ID
    /// </summary>
    public class Book_GetByIdHandler : IQueryHandler<Book_GetByIdQuery.V1, Book.V1.Output?>
    {
        private readonly ICoreDataContextFactory _coreDataContext;

        public Book_GetByIdHandler(ICoreDataContextFactory coreDataContext)
        {
            _coreDataContext = coreDataContext;
        }

        /// <inheritdoc/>
        public Book.V1.Output? Execute(Book_GetByIdQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<Book.V1.Output?> ExecuteAsync(Book_GetByIdQuery.V1 query, CancellationToken ctk = default)
        {
            await using var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
            return await ctx.ReadBookByIdAsync(query.Id, ctk).ConfigureAwait(false);
        }
    }
}