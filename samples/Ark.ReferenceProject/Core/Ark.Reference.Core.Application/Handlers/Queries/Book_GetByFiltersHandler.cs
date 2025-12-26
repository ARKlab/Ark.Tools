using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;
using Ark.Tools.Solid;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Queries
{
    /// <summary>
    /// Handler for retrieving Book entities by filters
    /// </summary>
    public class Book_GetByFiltersHandler : IQueryHandler<Book_GetByFiltersQuery.V1, PagedResult<Book.V1.Output>>
    {
        /// <inheritdoc/>
        public PagedResult<Book.V1.Output> Execute(Book_GetByFiltersQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public Task<PagedResult<Book.V1.Output>> ExecuteAsync(Book_GetByFiltersQuery.V1 query, CancellationToken ctk = default)
        {
            var (data, count) = InMemoryBookStore.GetByFilters(
                query.Id,
                query.Title,
                query.Author,
                query.Genre,
                query.Skip,
                query.Limit);

            return Task.FromResult(new PagedResult<Book.V1.Output>
            {
                Count = count,
                Data = data.ToList(),
                Limit = query.Limit,
                Skip = query.Skip,
                IsCountPartial = false
            });
        }
    }
}
