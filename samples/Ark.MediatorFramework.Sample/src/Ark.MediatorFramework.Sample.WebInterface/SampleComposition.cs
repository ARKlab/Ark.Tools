// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Rebus;
using Ark.Tools.Solid;


using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>
/// Hosting composition root. It layers the transport concerns (user context, Rebus, the
/// source-generated wrappers) on top of the transport-agnostic
/// <see cref="ApplicationComposition"/> domain graph, and starts the bus.
/// </summary>
public static class SampleComposition
{
    /// <summary>Builds and verifies the SimpleInjector container.</summary>
    /// <param name="network">The in-memory Rebus network to attach the transport to.</param>
    /// <returns>A verified container with the bus started.</returns>
    public static Container BuildContainer(InMemNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        // Transport-agnostic domain graph (handlers, store, cross-cutting decorator).
        ApplicationComposition.Register(container);

        // Transport user context: AspNetCore auth (HttpContext.User) with Rebus fallback.
        container.RegisterInstance<IHttpContextAccessor>(new HttpContextAccessor());
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, HostUserContextProvider>();

        // Source-generated Rebus message-handler wrappers for the selected requests.
        ArkGeneratedEndpoints.RegisterArkRebusHandlers(container);

        container.ConfigureRebus(cfg => cfg
            .Transport(t => t.UseInMemoryTransport(network, "ark.mediator.sample"))
            .Options(o =>
            {
                o.SetNumberOfWorkers(1);
                o.AutomaticallyFlowUserContext(container);
            }));

        container.Verify();
        container.StartBus();

        return container;
    }
}
