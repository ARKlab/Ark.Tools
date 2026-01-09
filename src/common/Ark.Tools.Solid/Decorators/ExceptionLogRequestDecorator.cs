// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;

using System;
using System.Threading;

<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Solid(net10.0)', Before:
namespace Ark.Tools.Solid.Decorators
{
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
=======
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
>>>>>>> After
using System.Threading.Tasks;

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