// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Provides the generated members of a messaging network declaration.</summary>
/// <typeparam name="TSelf">The implementing network declaration.</typeparam>
public interface IMessagingNetwork<TSelf>
    where TSelf : IMessagingNetwork<TSelf>
{
    /// <summary>Creates the resolved network options.</summary>
    /// <returns>The network options.</returns>
    static abstract MessagingNetworkOptions CreateOptions();

    /// <summary>Gets the generated contract registry.</summary>
    static abstract IMessagingContractRegistry Registry { get; }
}

/// <summary>Provides the generated members of a messaging participant declaration.</summary>
/// <typeparam name="TSelf">The implementing participant declaration.</typeparam>
public interface IMessagingParticipant<TSelf>
    where TSelf : IMessagingParticipant<TSelf>
{
    /// <summary>Creates the participant runtime descriptor.</summary>
    /// <param name="network">The resolved network options.</param>
    /// <param name="registry">The generated contract registry.</param>
    /// <returns>The participant descriptor.</returns>
    static abstract MessagingParticipantDescriptor CreateDescriptor(
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry);
}
