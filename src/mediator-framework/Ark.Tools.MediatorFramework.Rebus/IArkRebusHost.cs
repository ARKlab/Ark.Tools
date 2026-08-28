// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Rebus;

/// <summary>Defines the generated composition contract for a Rebus host.</summary>
public interface IArkRebusHost
{
    /// <summary>Registers generated handlers and the transport-neutral bus.</summary>
    /// <param name="container">The application container.</param>
    static abstract void Register(global::SimpleInjector.Container container);

    /// <summary>Configures generated owner-queue routes.</summary>
    /// <param name="routing">The Rebus routing configurer.</param>
    static abstract void ConfigureRouting(
        global::Rebus.Config.StandardConfigurer<global::Rebus.Routing.IRouter> routing);

    /// <summary>Configures generated retry options.</summary>
    /// <param name="options">The Rebus options configurer.</param>
    static abstract void ConfigureOptions(global::Rebus.Config.OptionsConfigurer options);

    /// <summary>Subscribes to the host's declared events.</summary>
    /// <param name="bus">The started Rebus bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing subscription completion.</returns>
    static abstract Task SubscribeAsync(
        global::Rebus.Bus.IBus bus,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the host's generated infrastructure requirements.</summary>
    /// <returns>The immutable participant requirements.</returns>
    static abstract ArkRebusParticipantRequirements GetRequirements();
}
