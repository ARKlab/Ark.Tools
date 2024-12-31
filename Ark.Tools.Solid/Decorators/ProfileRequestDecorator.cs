// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;

using NLog;

using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ProfileRequestDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        // We use Logger to trace the profile results. Could be written to a Db but I'm lazy atm.
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly IRequestHandler<TRequest, TResponse> _decorated;

        public ProfileRequestDecorator(IRequestHandler<TRequest, TResponse> decorated)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));

            _decorated = decorated;
        }

        public TResponse Execute(TRequest request)
        {
            Stopwatch stopWatch = new();
            stopWatch.Start();
            var result = _decorated.Execute(request);
            stopWatch.Stop();
            _logger.Trace(() => string.Format(CultureInfo.InvariantCulture, "Request<{Request}> executed in {Elapsed}ms", request.GetType(), stopWatch.ElapsedMilliseconds));

            return result;
        }

        public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
        {
            Stopwatch stopWatch = new();
            stopWatch.Start();
            var result = await _decorated.ExecuteAsync(request, ctk).ConfigureAwait(false);
            stopWatch.Stop();
            _logger.Trace(() => string.Format(CultureInfo.InvariantCulture, "Request<{Request}> executed in {Elapsed}ms", request.GetType(), stopWatch.ElapsedMilliseconds));

            return result;
        }
    }
}
