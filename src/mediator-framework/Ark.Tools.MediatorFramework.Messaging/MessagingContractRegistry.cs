// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Contract routing metadata implemented by a generated messaging network.</summary>
public interface IMessagingContractRegistry
{
    /// <summary>
    /// Gets the generated network identity, which must remain stable for the lifetime of the implementation.
    /// </summary>
    string NetworkIdentity { get; }

    /// <summary>Gets the destination for a contract.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    string GetDestination<T>() where T : class;

    /// <summary>Gets the processing participant identity for a message.</summary>
    /// <typeparam name="T">The message contract type.</typeparam>
    string GetProcessorIdentity<T>() where T : class;

    /// <summary>Gets the publishing participant identity for an event.</summary>
    /// <typeparam name="T">The event contract type.</typeparam>
    string GetPublisherIdentity<T>() where T : class;

    /// <summary>Gets the owner-selected protocol for a contract.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    SerializationProtocol GetWireProtocol<T>() where T : class;

    /// <summary>Gets the current logical name for a contract.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    string GetLogicalName<T>() where T : class;
}
