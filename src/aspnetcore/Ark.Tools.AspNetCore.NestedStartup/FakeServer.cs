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

    // NOTE: This implementation uses reflection on TContext which is provided by ASP.NET Core hosting infrastructure.
    // In production scenarios, TContext is DefaultHttpContext or similar types from Microsoft.AspNetCore.Http which are
    // preserved by the framework. This is a test/fake server implementation used only in development scenarios.
    // The UnconditionalSuppressMessage is used because the IServer interface doesn't have DynamicallyAccessedMembers,
    // preventing us from properly annotating this method. If this code is used in trimmed applications, the hosting
    // types must be preserved through root descriptors or the application will fail at runtime with clear exceptions.
    [UnconditionalSuppressMessage("Trimming", "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' requirements", Justification = "TContext is provided by ASP.NET Core hosting (typically DefaultHttpContext) which is preserved by the framework. This is a test server used in development. IServer interface prevents proper annotation. Runtime exceptions will occur if hosting types are trimmed.")]
    [UnconditionalSuppressMessage("Trimming", "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' requirements", Justification = "TContext is provided by ASP.NET Core hosting (typically DefaultHttpContext) which is preserved by the framework. This is a test server used in development. IServer interface prevents proper annotation. Runtime exceptions will occur if hosting types are trimmed.")]
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