// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;

using Ark.Tools.Rebus;
using Ark.Tools.Solid;

using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.Api;

/// <summary>
/// Composition root. Wires the pure handlers, the cross-cutting decorator, the in-memory
/// store and the Rebus in-memory transport into a single SimpleInjector container.
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

        container.RegisterSingleton<IGreetingStore, InMemoryGreetingStore>();
        container.RegisterSingleton<AuditCounter>();
        container.RegisterSingleton<IUserContext, DefaultUserContext>();

        container.Register<IRequestHandler<CreateGreetingRequest, GreetingResponse>, CreateGreetingHandler>();
        container.Register<IQueryHandler<GetGreetingQuery, GreetingResponse>, GetGreetingHandler>();

        // Cross-cutting concern applied transport-agnostically.
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(AuditRequestDecorator<,>));

        // Source-generated Rebus message-handler wrappers.
        ArkGeneratedEndpoints.RegisterArkRebusHandlers(container);

        container.ConfigureRebus(cfg => cfg
            .Transport(t => t.UseInMemoryTransport(network, "ark.mediator.sample"))
            .Options(o => o.SetNumberOfWorkers(1)));

        container.Verify();
        container.StartBus();

        return container;
    }
}
