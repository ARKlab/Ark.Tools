// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ProfileQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        // We use Logger to trace the profile results. Could be written to a Db but I'm lazy atm.
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly IQueryHandler<TQuery, TResult> _decorated;

        public ProfileQueryDecorator(IQueryHandler<TQuery, TResult> decorated)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));

            _decorated = decorated;
        }

        public TResult Execute(TQuery query)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = _decorated.Execute(query);
            stopWatch.Stop();
            _logger.Trace("Query<{Query}> executed in {Elapsed}ms", query.GetType(), stopWatch.ElapsedMilliseconds);

            return result;
        }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = await _decorated.ExecuteAsync(query, ctk);
            stopWatch.Stop();
            _logger.Trace("Query<{Query}> executed in {Elapsed}ms", query.GetType(), stopWatch.ElapsedMilliseconds);

            return result;
        }
    }
}
