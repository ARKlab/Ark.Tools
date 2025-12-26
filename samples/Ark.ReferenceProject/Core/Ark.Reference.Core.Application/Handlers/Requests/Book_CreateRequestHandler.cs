using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

using EnsureThat;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests
{
    /// <summary>
    /// Handler for creating a new Book entity
    /// </summary>
    public class Book_CreateRequestHandler : IRequestHandler<Book_CreateRequest.V1, Book.V1.Output>
    {
        /// <inheritdoc/>
        public Book.V1.Output Execute(Book_CreateRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public Task<Book.V1.Output> ExecuteAsync(Book_CreateRequest.V1 request, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(request.Data, nameof(request.Data));
            return Task.FromResult(InMemoryBookStore.Create(request.Data));
        }
    }
}
