// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>
/// Transport-agnostic composition of the application layer: the pure handlers, the shared store
/// and the cross-cutting decorator. The hosting layer adds the transport concerns (user context,
/// Minimal API endpoints, Rebus) on top of this registration.
/// </summary>
public static class ApplicationComposition
{
    /// <summary>Registers the pure domain graph into the given container.</summary>
    /// <param name="container">The SimpleInjector container to register into.</param>
    public static void Register(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        container.RegisterSingleton<IGreetingStore, InMemoryGreetingStore>();
        container.RegisterSingleton<AuditCounter>();

        container.Register<IRequestHandler<CreateGreetingRequest, GreetingResponse>, CreateGreetingHandler>();
        container.Register<IQueryHandler<GetGreetingQuery, GreetingResponse>, GetGreetingHandler>();
        container.Register<IRequestHandler<UploadGreetingCardRequest, UploadResponse>, UploadGreetingCardHandler>();

        // Cross-cutting concern applied transport-agnostically.
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(AuditRequestDecorator<,>));
    }
}
