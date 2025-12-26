using Ark.Reference.Core.API.Queries;
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
        /// <inheritdoc/>
        public Book.V1.Output? Execute(Book_GetByIdQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public Task<Book.V1.Output?> ExecuteAsync(Book_GetByIdQuery.V1 query, CancellationToken ctk = default)
        {
            return Task.FromResult(InMemoryBookStore.GetById(query.Id));
        }
    }
}
