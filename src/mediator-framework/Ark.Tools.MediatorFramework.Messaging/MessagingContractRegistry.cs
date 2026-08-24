// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Generated routing and ownership metadata for one messaging network.</summary>
public sealed class MessagingContractRegistry
{
    private readonly Func<Type, string> _destination;
    private readonly Func<Type, string> _processor;
    private readonly Func<Type, string> _publisher;
    private readonly Func<Type, SerializationProtocol> _protocol;
    private readonly Func<Type, string> _logicalName;

    /// <summary>Creates a routing registry from generated lookup functions.</summary>
    /// <param name="networkIdentity">The generated network identity.</param>
    /// <param name="destination">Gets the destination for a contract.</param>
    /// <param name="processor">Gets the processing participant identity.</param>
    /// <param name="publisher">Gets the publishing participant identity.</param>
    /// <param name="protocol">Gets the owner-selected serialization protocol.</param>
    /// <param name="logicalName">Gets the current logical contract name.</param>
    public MessagingContractRegistry(
        string networkIdentity,
        Func<Type, string> destination,
        Func<Type, string> processor,
        Func<Type, string> publisher,
        Func<Type, SerializationProtocol> protocol,
        Func<Type, string> logicalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(networkIdentity);
        NetworkIdentity = networkIdentity;
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _logicalName = logicalName ?? throw new ArgumentNullException(nameof(logicalName));
    }

    /// <summary>Gets the generated network identity.</summary>
    public string NetworkIdentity { get; }

    /// <summary>Gets the destination for a contract.</summary>
    public string GetDestination(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        return _destination(contractType);
    }

    /// <summary>Gets the processing participant identity for a message.</summary>
    public string GetProcessorIdentity(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        return _processor(contractType);
    }

    /// <summary>Gets the publishing participant identity for an event.</summary>
    public string GetPublisherIdentity(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        return _publisher(contractType);
    }

    /// <summary>Gets the owner-selected protocol for a contract.</summary>
    public SerializationProtocol GetWireProtocol(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        return _protocol(contractType);
    }

    /// <summary>Gets the current logical name for a contract.</summary>
    public string GetLogicalName(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        return _logicalName(contractType);
    }
}
