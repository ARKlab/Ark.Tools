﻿// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ExceptionLogQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>, IDisposable
    {
        private readonly IQueryHandler<TQuery, TResult> _decorated;
        private readonly ILogger _logger;

        public ExceptionLogQueryDecorator(IQueryHandler<TQuery, TResult> decorated)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));

            _decorated = decorated;
            _logger = LogManager.GetLogger(_decorated.GetType().ToString());
        }

        public TResult Execute(TQuery query)
        {            
            try
            {
                return _decorated.Execute(query);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception occured");
                throw;
            }
        }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            try
            {
                return await _decorated.ExecuteAsync(query, ctk);
            } catch (Exception ex)
            {
                _logger.Error(ex, "Exception occured");
                throw;
            }
        }
    }
}
