using Ark.Tools.Solid;

using Polly;
using Polly.Retry;


namespace Ark.Reference.Common.Services.Decorators;

public sealed class OptimisticConcurrencyRetrierDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IRequestHandler<TRequest, TResult> _inner;

    private static readonly ResiliencePipeline _retry = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(static ex => ex.IsOptimistic()),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.Zero,
        })
        .Build();

    public OptimisticConcurrencyRetrierDecorator(IRequestHandler<TRequest, TResult> inner)
    {
        _inner = inner;
    }

    public async Task<TResult> ExecuteAsync(TRequest Request, CancellationToken ctk = default)
    {
        return await _retry
            .ExecuteAsync(async ct => await _inner.ExecuteAsync(Request, ct).ConfigureAwait(false), ctk).ConfigureAwait(false);
    }

}