// Copyright (c) 2018 Ark S.r.l. All rights reserved.
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

        public ExceptionLogQueryDecorator(IQueryHandler<TQuery, TResult> decorated)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));

            _decorated = decorated;
        }

        public TResult Execute(TQuery query)
        {            
            try
            {
                return _decorated.Execute(query);
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
                logger.Error(ex, "Exception occured");
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
                Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
                logger.Error(ex, "Exception occured");
                throw;
            }
        }
    }
}
