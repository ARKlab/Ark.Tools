// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 


namespace Ark.Tools.Solid.Decorators;

public sealed class ExceptionLogRequestDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _decorated;

    public ExceptionLogRequestDecorator(IRequestHandler<TRequest, TResponse> decorated)
    {
        ArgumentNullException.ThrowIfNull(decorated);

        _decorated = decorated;
    }

    public TResponse Execute(TRequest request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        try
        {
            return _decorated.Execute(request);
        }
        catch (Exception ex)
        {
            Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
            logger.Error(ex, "Exception occured");
            throw;
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
    {
        try
        {
            return await _decorated.ExecuteAsync(request, ctk).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
            logger.Error(ex, "Exception occured");
            throw;
        }

    }
}