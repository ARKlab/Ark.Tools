using Ark.Tools.Solid;

using Polly;


namespace Ark.Reference.Common.Services.Decorators;

public sealed class OptimisticConcurrencyRetrierDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IRequestHandler<TRequest, TResult> _inner;

    public OptimisticConcurrencyRetrierDecorator(IRequestHandler<TRequest, TResult> inner)
    {
        _inner = inner;
    }

    public async Task<TResult> ExecuteAsync(TRequest Request, CancellationToken ctk = default)
    {
        return await Policy.Handle<Exception>(ex => ex.IsOptimistic())
            .RetryAsync(2)
            .ExecuteAsync(ct => _inner.ExecuteAsync(Request, ct), ctk).ConfigureAwait(false);
    }

}