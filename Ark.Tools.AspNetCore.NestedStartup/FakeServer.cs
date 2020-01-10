// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Threading;
using System.Threading.Tasks;
//using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Ark.Tools.AspNetCore.NestedStartup
{
    public sealed class FakeServer : IServer
    {
        private Func<HttpContext, Task> _process;

        public FakeServer(IFeatureCollection featureCollection)
        {
            Features = featureCollection;
        }

        public IFeatureCollection Features { get; }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            var prop = typeof(TContext).GetProperty("HttpContext");

            _process = (HttpContext ctx) =>
            {
                var ccc = Activator.CreateInstance<TContext>();
                prop.SetValue(ccc, ctx);

                return application.ProcessRequestAsync(ccc);
            };

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task Process(HttpContext ctx)
            => _process(ctx);
    }


}
