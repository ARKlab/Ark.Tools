// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Retry;
using Ark.Tools.Solid;


using Rebus.Handlers;
using Rebus.Serialization.Json;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;
using System.Text.Json;

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
    /// <param name="useProtobufRebus">Whether Rebus messages use Protobuf instead of JSON.</param>
    /// <returns>A verified container with the bus started.</returns>
    public static Container BuildContainer(InMemNetwork network, bool useProtobufRebus = false)
    {
        ArgumentNullException.ThrowIfNull(network);

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        // Transport-agnostic domain graph (handlers, store, cross-cutting decorator).
        ApplicationComposition.Register(container);

        // Transport user context: AspNetCore auth (HttpContext.User) with Rebus fallback.
        container.RegisterInstance<IHttpContextAccessor>(new HttpContextAccessor());
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, HostUserContextProvider>();

        // Source-generated Rebus message-handler wrappers for the [RebusMessage] requests.
        ArkGeneratedEndpoints.RegisterArkRebusHandlers(container);

        // The Rebus pipeline opens the SimpleInjector scope per message (no Rebus unit-of-work needed):
        // decorating the generated handlers gives each dispatched message its own request-equivalent scope.
        container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));

        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(t => t.UseInMemoryTransport(network, "ark.mediator.sample"));

            if (useProtobufRebus)
                cfg.Serialization(s => s.Register(_ => new ProtobufRebusSerializer(typeof(CreateGreetingRequest))));
            else
                cfg.Serialization(s => s.UseSystemTextJson(new JsonSerializerOptions().ConfigureArkDefaults()));

            cfg.Options(o =>
            {
                o.SetNumberOfWorkers(1);
                o.AutomaticallyFlowUserContext(container);
                // Fail fast to the dead-letter (error) queue: one delivery attempt, then forward with headers.
                o.ArkRetryStrategy(maxDeliveryAttempts: 1);
            });
        });

        container.Verify();
        container.StartBus();

        return container;
    }
}
