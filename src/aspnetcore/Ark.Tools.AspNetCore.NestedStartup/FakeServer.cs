// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

//using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Ark.Tools.AspNetCore.NestedStartup;

public sealed class FakeServer : IServer
{
    private Func<HttpContext, Task>? _process;

    public FakeServer(IFeatureCollection featureCollection)
    {
        Features = featureCollection;
    }

    public IFeatureCollection Features { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' requirements", Justification = "StartAsync is constrained by IServer interface. The TContext type is controlled by ASP.NET Core hosting and has a well-known structure with HttpContext property.")]
    [UnconditionalSuppressMessage("Trimming", "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' requirements", Justification = "StartAsync is constrained by IServer interface. The TContext type is controlled by ASP.NET Core hosting and has a well-known structure with HttpContext property.")]
    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken) where TContext : notnull
    {
        var prop = typeof(TContext).GetProperty("HttpContext") ?? throw new InvalidOperationException("TContext do not expose HttpContext property");

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
        => _process?.Invoke(ctx) ?? throw new InvalidOperationException("Server has not been Started");
}