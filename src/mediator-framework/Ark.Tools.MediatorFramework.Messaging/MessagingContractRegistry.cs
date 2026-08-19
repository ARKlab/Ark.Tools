// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Explicit generated-registry entry for one messaging contract.</summary>
public sealed class MessagingContractDescriptor
{
    /// <summary>Creates a contract descriptor.</summary>
    public MessagingContractDescriptor(
        Type contractType,
        string name,
        SerializationProtocol defaultSerializer,
        IEnumerable<string>? formerNames = null)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentException.ThrowIfNullOrEmpty(name);

        ContractType = contractType;
        Name = name;
        DefaultSerializer = defaultSerializer;
        FormerNames = new ReadOnlyCollection<string>(
            (formerNames ?? Array.Empty<string>())
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(alias => alias, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>Gets the registered CLR contract type.</summary>
    public Type ContractType { get; }

    /// <summary>Gets the current logical wire name.</summary>
    public string Name { get; }

    /// <summary>Gets the owner-selected write protocol.</summary>
    public SerializationProtocol DefaultSerializer { get; }

    /// <summary>Gets the ordinal-sorted former names accepted on receive.</summary>
    public IReadOnlyList<string> FormerNames { get; }
}

/// <summary>Resolves contracts only from explicit generated-style registry entries.</summary>
public sealed class MessagingContractRegistry
{
    private readonly Dictionary<string, MessagingContractDescriptor> _byName;
    private readonly Dictionary<Type, MessagingContractDescriptor> _byType;

    /// <summary>Creates an empty contract registry.</summary>
    public MessagingContractRegistry(IEnumerable<MessagingContractDescriptor>? descriptors = null)
    {
        _byName = new Dictionary<string, MessagingContractDescriptor>(StringComparer.Ordinal);
        _byType = new Dictionary<Type, MessagingContractDescriptor>();
        foreach (var descriptor in descriptors ?? Array.Empty<MessagingContractDescriptor>())
            Register(descriptor);
    }

    /// <summary>Registers a contract and its former-name aliases.</summary>
    public void Register(MessagingContractDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!_byType.TryAdd(descriptor.ContractType, descriptor))
            throw new InvalidOperationException($"Contract type '{descriptor.ContractType.FullName ?? descriptor.ContractType.Name}' is already registered.");
        if (!_byName.TryAdd(descriptor.Name, descriptor))
        {
            _byType.Remove(descriptor.ContractType);
            throw new InvalidOperationException($"Contract name '{descriptor.Name}' is already registered.");
        }

        foreach (var alias in descriptor.FormerNames)
        {
            if (!_byName.TryAdd(alias, descriptor))
            {
                _byName.Remove(descriptor.Name);
                foreach (var registeredAlias in descriptor.FormerNames)
                    _byName.Remove(registeredAlias);
                _byType.Remove(descriptor.ContractType);
                throw new InvalidOperationException($"Contract alias '{alias}' is already registered.");
            }
        }
    }

    /// <summary>Resolves a current name or former-name alias.</summary>
    public MessagingContractDescriptor Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_byName.TryGetValue(name, out var descriptor))
            return descriptor;
        throw new MessagingEnvelopeException(MessagingFailureKind.UnknownContract, "The envelope contract is not registered.", MessagingHeaderNames.MessageType);
    }

    /// <summary>Resolves a registered CLR contract type.</summary>
    public MessagingContractDescriptor Resolve(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        if (_byType.TryGetValue(contractType, out var descriptor))
            return descriptor;
        throw new MessagingEnvelopeException(MessagingFailureKind.UnknownContract, "The contract type is not registered.");
    }

    /// <summary>Tries to resolve a current name or former-name alias.</summary>
    public bool TryResolve(string name, out MessagingContractDescriptor? descriptor)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _byName.TryGetValue(name, out descriptor);
    }

    /// <summary>Gets all current contract descriptors in deterministic order.</summary>
    public IReadOnlyList<MessagingContractDescriptor> Descriptors
    {
        get
        {
            return _byType.Values.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        }
    }
}
