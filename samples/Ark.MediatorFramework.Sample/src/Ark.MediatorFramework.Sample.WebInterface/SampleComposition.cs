// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Retry;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Nodatime.Protobuf;

using Rebus.Handlers;
using Rebus.Config;
using Rebus.Serialization.Json;
using Rebus.Transport.InMem;

using ProtoBuf.Meta;

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
    /// <summary>Builds the SimpleInjector container before ASP.NET Core integration completes it.</summary>
    /// <param name="network">The in-memory Rebus network to attach the transport to.</param>
    /// <param name="useProtobufRebus">Whether Rebus messages use Protobuf instead of JSON.</param>
    /// <returns>The configured container. Hosting verifies it and starts the bus after integration.</returns>
    public static Container BuildContainer(InMemNetwork network, bool useProtobufRebus = false)
    {
        ArgumentNullException.ThrowIfNull(network);

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        // Transport-agnostic domain graph (handlers, store, cross-cutting decorator).
        ApplicationComposition.Register(container);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();

        // Transport user context: AspNetCore auth (HttpContext.User) with Rebus fallback.
        // IHttpContextAccessor is forwarded from Microsoft DI by SampleStartup when the
        // SimpleInjector container locks, after ASP.NET Core has built its service provider.
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, HostUserContextProvider>();

        // Source-generated Rebus message-handler wrappers for the [RebusMessage] requests.
        ArkGeneratedEndpoints.RegisterArkRebusHandlers(container);

        // The Rebus pipeline opens the SimpleInjector scope per message (no Rebus unit-of-work needed):
        // decorating the generated handlers gives each dispatched message its own request-equivalent scope.
        container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));

        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(t => t.UseInMemoryTransport(network, "ark.mediator.sample"));
            cfg.Routing(ArkGeneratedEndpoints.ConfigureArkRebusRouting);

            if (useProtobufRebus)
            {
                var model = RuntimeTypeModel.Create().AddNodaTimeSurrogates();
                cfg.Serialization(s => s.UseProtobuf(model));
            }
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

        return container;
    }
}
