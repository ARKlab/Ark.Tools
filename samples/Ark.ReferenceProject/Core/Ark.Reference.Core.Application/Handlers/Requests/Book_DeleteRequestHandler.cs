using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Tools.Solid;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests
{
    /// <summary>
    /// Handler for deleting a Book entity
    /// </summary>
    public class Book_DeleteRequestHandler : IRequestHandler<Book_DeleteRequest.V1, bool>
    {
        private readonly ICoreDataContextFactory _coreDataContext;

        public Book_DeleteRequestHandler(ICoreDataContextFactory coreDataContext)
        {
            ArgumentNullException.ThrowIfNull(coreDataContext);
            _coreDataContext = coreDataContext;
        }

        /// <inheritdoc/>
        public bool Execute(Book_DeleteRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<bool> ExecuteAsync(Book_DeleteRequest.V1 request, CancellationToken ctk = default)
        {
            await using var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);

            var existing = await ctx.ReadBookByIdAsync(request.Id, ctk).ConfigureAwait(false);
            if (existing == null)
                return false;

            await ctx.DeleteBookAsync(request.Id, ctk).ConfigureAwait(false);
            await ctx.CommitAsync(ctk).ConfigureAwait(false);

            return true;
        }
    }
}
