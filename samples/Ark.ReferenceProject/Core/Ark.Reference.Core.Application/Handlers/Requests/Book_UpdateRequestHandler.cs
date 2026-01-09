using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests
{
    /// <summary>
    /// Handler for updating a Book entity
    /// </summary>
    public class Book_UpdateRequestHandler : IRequestHandler<Book_UpdateRequest.V1, Book.V1.Output?>
    {
        private readonly ICoreDataContextFactory _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public Book_UpdateRequestHandler(
            ICoreDataContextFactory coreDataContext,
            IContextProvider<ClaimsPrincipal> userContext)
        {
            ArgumentNullException.ThrowIfNull(coreDataContext);
            ArgumentNullException.ThrowIfNull(userContext);

            _coreDataContext = coreDataContext;
            _userContext = userContext;
        }

        /// <inheritdoc/>
        public Book.V1.Output? Execute(Book_UpdateRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<Book.V1.Output?> ExecuteAsync(Book_UpdateRequest.V1 request, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(request.Data);

            await using var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);

            var existing = await ctx.ReadBookByIdAsync(request.Id, ctk).ConfigureAwait(false);
            if (existing == null)
                return null;

            await ctx.EnsureAudit(AuditKind.Book, _userContext.GetUserId(), "Update Book", ctk).ConfigureAwait(false);

            var updateBookData = new Book.V1.Output
            {
                Id = request.Id,
                Title = request.Data.Title,
                Author = request.Data.Author,
                Genre = request.Data.Genre,
                ISBN = request.Data.ISBN,
                Description = $"Book updated: {request.Data.Title} by {request.Data.Author}"
            };

            await ctx.PutBookAsync(updateBookData, ctk).ConfigureAwait(false);

            var entity = await ctx.ReadBookByIdAsync(request.Id, ctk).ConfigureAwait(false);

            await ctx.CommitAsync(ctk).ConfigureAwait(false);

            return entity;
        }
    }
}