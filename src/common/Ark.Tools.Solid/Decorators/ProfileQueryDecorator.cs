// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using NLog;

using System.Diagnostics;
using System.Globalization;
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
            ArgumentNullException.ThrowIfNull(decorated);

            _decorated = decorated;
        }

        public TResult Execute(TQuery query)
        {
            Stopwatch stopWatch = new();
            stopWatch.Start();
            var result = _decorated.Execute(query);
            stopWatch.Stop();
            _logger.Trace(CultureInfo.InvariantCulture, "Query<{Query}> executed in {Elapsed}ms", query.GetType(), stopWatch.ElapsedMilliseconds);

            return result;
        }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            Stopwatch stopWatch = new();
            stopWatch.Start();
            var result = await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
            stopWatch.Stop();
            _logger.Trace(CultureInfo.InvariantCulture, "Query<{Query}> executed in {Elapsed}ms", query.GetType(), stopWatch.ElapsedMilliseconds);

            return result;
        }
    }
}