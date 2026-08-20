// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Describes one statically known messaging contract.</summary>
public abstract class MessagingContractDescriptor
{
    /// <summary>Creates a contract descriptor.</summary>
    protected MessagingContractDescriptor(
        string name,
        SerializationProtocol defaultSerializer,
        IEnumerable<string>? formerNames)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        DefaultSerializer = defaultSerializer;
        FormerNames = (formerNames ?? Array.Empty<string>())
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets the current logical wire name.</summary>
    public string Name { get; }

    /// <summary>Gets the owner-selected write protocol.</summary>
    public SerializationProtocol DefaultSerializer { get; }

    /// <summary>Gets the ordinal-sorted former names accepted on receive.</summary>
    public IReadOnlyList<string> FormerNames { get; }

    [SuppressMessage("Naming", "IDE1006", Justification = "Internal generated dispatch members use the public member naming convention.")]
    internal abstract Type ContractType { get; }

}

/// <summary>Describes one statically known messaging contract of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The message or event payload type.</typeparam>
public sealed class MessagingContractDescriptor<T> : MessagingContractDescriptor
    where T : notnull
{
    /// <summary>Creates a typed contract descriptor.</summary>
    public MessagingContractDescriptor(
        string name,
        SerializationProtocol defaultSerializer,
        IEnumerable<string>? formerNames = null)
        : base(name, defaultSerializer, formerNames)
    {
    }

    [SuppressMessage("Naming", "IDE1006", Justification = "Internal generated dispatch members use the public member naming convention.")]
    internal override Type ContractType => typeof(T);

}

/// <summary>Resolves only statically registered messaging contracts.</summary>
public sealed class MessagingContractRegistry
{
    private readonly FrozenDictionary<string, MessagingContractDescriptor> _byName;
    private readonly FrozenDictionary<Type, string> _namesByType;

    /// <summary>Creates an immutable contract registry.</summary>
    public MessagingContractRegistry(IEnumerable<MessagingContractDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var byName = new Dictionary<string, MessagingContractDescriptor>(StringComparer.Ordinal);
        var namesByType = new Dictionary<Type, string>();
        foreach (var descriptor in descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (!namesByType.TryAdd(descriptor.ContractType, descriptor.Name))
                throw new InvalidOperationException("A contract type is already registered.");
            _addName(byName, descriptor.Name, descriptor);
            foreach (var alias in descriptor.FormerNames)
                _addName(byName, alias, descriptor);
        }

        _byName = byName.ToFrozenDictionary(StringComparer.Ordinal);
        _namesByType = namesByType.ToFrozenDictionary();
    }

    /// <summary>Resolves a current name or former-name alias.</summary>
    public MessagingContractDescriptor Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_byName.TryGetValue(name, out var descriptor))
            return descriptor;
        throw new MessagingProtocolException(MessagingFailureKind.UnknownContract, "The message contract is not registered.", MessagingHeaderNames.MessageType);
    }

    /// <summary>Resolves the statically registered descriptor for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The message or event payload type.</typeparam>
    public MessagingContractDescriptor<T> Resolve<T>()
        where T : notnull
    {
        if (_namesByType.TryGetValue(typeof(T), out var name)
            && _byName[name] is MessagingContractDescriptor<T> descriptor)
            return descriptor;
        throw new MessagingProtocolException(MessagingFailureKind.UnknownContract, "The contract type is not registered.");
    }

    /// <summary>Gets all current contract descriptors in deterministic order.</summary>
    public IReadOnlyList<MessagingContractDescriptor> Descriptors
        => _namesByType.Values
            .Select(name => _byName[name])
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();

    private static void _addName(
        IDictionary<string, MessagingContractDescriptor> contracts,
        string name,
        MessagingContractDescriptor descriptor)
    {
        if (!contracts.TryAdd(name, descriptor))
            throw new InvalidOperationException("A contract name or alias is already registered.");
    }
}
