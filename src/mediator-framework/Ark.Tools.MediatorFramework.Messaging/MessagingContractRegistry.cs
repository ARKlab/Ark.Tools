// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Contract routing metadata implemented by a generated messaging network.</summary>
public interface IMessagingContractRegistry
{
    /// <summary>Gets the generated network identity.</summary>
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

/// <summary>Generated routing and ownership metadata for one messaging network.</summary>
public sealed class MessagingContractRegistry
{
    private readonly IMessagingContractRegistry _registry;

    /// <summary>Creates a routing registry from generated network metadata.</summary>
    /// <param name="registry">The generated network metadata.</param>
    public MessagingContractRegistry(IMessagingContractRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentException.ThrowIfNullOrEmpty(registry.NetworkIdentity);
        NetworkIdentity = registry.NetworkIdentity;
    }

    /// <summary>Gets the generated network identity.</summary>
    public string NetworkIdentity { get; }

    /// <summary>Gets the destination for a contract.</summary>
    public string GetDestination<T>() where T : class
    {
        return _registry.GetDestination<T>();
    }

    /// <summary>Gets the processing participant identity for a message.</summary>
    public string GetProcessorIdentity<T>() where T : class
    {
        return _registry.GetProcessorIdentity<T>();
    }

    /// <summary>Gets the publishing participant identity for an event.</summary>
    public string GetPublisherIdentity<T>() where T : class
    {
        return _registry.GetPublisherIdentity<T>();
    }

    /// <summary>Gets the owner-selected protocol for a contract.</summary>
    public SerializationProtocol GetWireProtocol<T>() where T : class
    {
        return _registry.GetWireProtocol<T>();
    }

    /// <summary>Gets the current logical name for a contract.</summary>
    public string GetLogicalName<T>() where T : class
    {
        return _registry.GetLogicalName<T>();
    }
}
