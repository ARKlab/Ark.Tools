using Ark.Tools.Solid;

using Polly;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Services.Decorators;

public sealed class OptimisticConcurrencyRetrierDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IRequestHandler<TRequest, TResult> _inner;

    public OptimisticConcurrencyRetrierDecorator(IRequestHandler<TRequest, TResult> inner)
    {
        _inner = inner;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "Sync method")]
    public TResult Execute(TRequest Request)
    {
        return Policy.Handle<Exception>(ex => ex.IsOptimistic())
            .Retry(2)
            .Execute(() => _inner.Execute(Request));
    }

    public async Task<TResult> ExecuteAsync(TRequest Request, CancellationToken ctk = default)
    {
        return await Policy.Handle<Exception>(ex => ex.IsOptimistic())
            .RetryAsync(2)
            .ExecuteAsync(ct => _inner.ExecuteAsync(Request, ct), ctk).ConfigureAwait(false);
    }

}