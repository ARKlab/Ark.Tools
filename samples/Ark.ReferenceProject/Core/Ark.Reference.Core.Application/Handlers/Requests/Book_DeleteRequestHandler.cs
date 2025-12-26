using Ark.Reference.Core.API.Requests;
using Ark.Tools.Solid;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests
{
    /// <summary>
    /// Handler for deleting a Book entity
    /// </summary>
    public class Book_DeleteRequestHandler : IRequestHandler<Book_DeleteRequest.V1, bool>
    {
        /// <inheritdoc/>
        public bool Execute(Book_DeleteRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public Task<bool> ExecuteAsync(Book_DeleteRequest.V1 request, CancellationToken ctk = default)
        {
            return Task.FromResult(InMemoryBookStore.Delete(request.Id));
        }
    }
}
