// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 



namespace Ark.Tools.Solid.Decorators;

public sealed class ExceptionLogQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>, IDisposable
{
    private readonly IQueryHandler<TQuery, TResult> _decorated;
    private readonly ILogger _logger;

    public ExceptionLogQueryDecorator(IQueryHandler<TQuery, TResult> decorated)
    {
        ArgumentNullException.ThrowIfNull(decorated);

        _decorated = decorated;
        _logger = LogManager.GetLogger(_decorated.GetType().ToString());
    }

    public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
    {
        try
        {
            return await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception occured");
            throw;
        }
    }
}