// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Threading;
using System.Threading.Tasks;
//using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Ark.Tools.AspNetCore.NestedStartup
{
    public sealed class FakeServer : IServer
    {
        private IHttpApplication<HttpContextAccessor> _application;

        public FakeServer(IFeatureCollection featureCollection)
        {
            Features = featureCollection;
        }

        public IFeatureCollection Features { get; }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            _application = (IHttpApplication<HttpContextAccessor>)application;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public async Task Process(HttpContext ctx)
        {
            var c = new HttpContextAccessor();

            c.HttpContext = ctx;

            await _application.ProcessRequestAsync(c);
        }
    }
}
