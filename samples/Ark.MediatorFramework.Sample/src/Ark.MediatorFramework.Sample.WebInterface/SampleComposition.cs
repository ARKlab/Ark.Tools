// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;
using NodaTime;

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
    /// <param name="useSqlStore">Whether to use SQL persistence and the outbox.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="clock">Optional clock override used by tests.</param>
    /// <param name="greetingStore">
    /// Optional pre-built store shared with the processor container. When <see langword="null"/>
    /// and <paramref name="useSqlStore"/> is <see langword="false"/>, a new in-memory store is created.
    /// </param>
    /// <returns>The configured container. Hosting verifies it and starts the bus after integration.</returns>
    public static Container BuildContainer(
        InMemNetwork network,
        bool useSqlStore = true,
        string? connectionString = null,
        IClock? clock = null,
        IGreetingStore? greetingStore = null)
    {
        ArgumentNullException.ThrowIfNull(network);

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.RegisterInstance(network);

        // Transport-agnostic domain graph (handlers, store, cross-cutting decorator).
        ApplicationComposition.Register(container, useSqlStore, connectionString, clock, greetingStore);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();

        // Transport user context: AspNetCore auth (HttpContext.User) with Rebus fallback.
        // IHttpContextAccessor is forwarded from Microsoft DI by SampleStartup when the
        // SimpleInjector container locks, after ASP.NET Core has built its service provider.
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, HostUserContextProvider>();

        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(t =>
            {
                t.UseDrainableInMemoryTransportAsOneWayClient(network);
                if (useSqlStore)
                    ApplicationComposition.ConfigureRebusOutbox(t, container, startProcessor: false);
            });
            ApplicationComposition.ConfigureRebusCommon(cfg, container, SampleRebusEndpoints.ConfigureRouting);
        });

        return container;
    }
}
