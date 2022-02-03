// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using System.Diagnostics;
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
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = _decorated.Execute(request);
            stopWatch.Stop();
            _logger.Trace(() => string.Format("Request<{0}> executed in {1}ms", request.GetType(), stopWatch.ElapsedMilliseconds));

            return result;
        }

        public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default(CancellationToken))
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = await _decorated.ExecuteAsync(request, ctk);
            stopWatch.Stop();
            _logger.Trace(() => string.Format("Request<{0}> executed in {1}ms", request.GetType(), stopWatch.ElapsedMilliseconds));

            return result;
        }
    }
}
