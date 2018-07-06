using Ark.Tools.Solid.Abstractions;
using EnsureThat;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class AuthorizeQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _decorated;
        private readonly IAuthorizer<TQuery> _authorizer;        

        public AuthorizeQueryDecorator(IQueryHandler<TQuery, TResult> decorated, IAuthorizer<TQuery> authorizer)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(authorizer, nameof(authorizer));

            _decorated = decorated;
            _authorizer = authorizer;
        }

        public TResult Execute(TQuery query)
        {
            _authorizer.AuthorizeOrThrow(query);
            return _decorated.Execute(query);
        }

        public Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default(CancellationToken))
        {
            _authorizer.AuthorizeOrThrow(query);
            return _decorated.ExecuteAsync(query, ctk);
        }
    }
}
