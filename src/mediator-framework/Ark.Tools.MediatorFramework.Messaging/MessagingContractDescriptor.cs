// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Messaging;

/// <summary>Immutable metadata for one registered message or event contract.</summary>
public sealed record MessagingContractDescriptor
{
    /// <summary>Creates contract metadata.</summary>
    public MessagingContractDescriptor(
        Type contractType,
        bool isEvent,
        string owner,
        string name,
        IReadOnlyList<string> formerNames,
        SerializationProtocol? serializer)
    {
        ContractType = contractType ?? throw new ArgumentNullException(nameof(contractType));
        IsEvent = isEvent;
        Owner = string.IsNullOrWhiteSpace(owner) ? throw new ArgumentException("The owner cannot be blank.", nameof(owner)) : owner;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("The name cannot be blank.", nameof(name)) : name;
        FormerNames = (formerNames ?? throw new ArgumentNullException(nameof(formerNames))).ToArray();
        Serializer = serializer;
    }

    /// <summary>CLR contract type.</summary>
    public Type ContractType { get; }
    /// <summary>Whether the contract is an event.</summary>
    public bool IsEvent { get; }
    /// <summary>Queue owner for messages or publisher owner for events.</summary>
    public string Owner { get; }
    /// <summary>Current normalized logical contract name.</summary>
    public string Name { get; }
    /// <summary>Former normalized names accepted on receive.</summary>
    public IReadOnlyList<string> FormerNames { get; }
    /// <summary>Explicit serializer, or <see langword="null"/> for the network default.</summary>
    public SerializationProtocol? Serializer { get; }

    /// <summary>Creates metadata from a reflected contract attribute.</summary>
    public static MessagingContractDescriptor Resolve(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        var message = Attribute.GetCustomAttribute(contractType, typeof(MessageAttribute)) as MessageAttribute;
        var @event = Attribute.GetCustomAttribute(contractType, typeof(EventAttribute)) as EventAttribute;
        if ((message is null) == (@event is null))
            throw new InvalidOperationException($"Contract '{contractType.FullName}' must have exactly one messaging attribute.");

        var owner = message?.OwnerQueue ?? @event!.OwnerPublisher;
        if (string.IsNullOrWhiteSpace(owner))
            throw new InvalidOperationException($"Contract '{contractType.FullName}' must declare a non-blank owner.");
        var explicitName = message?.Name ?? @event?.Name;
        var name = explicitName ?? Normalize(contractType);
        var formerNames = message?.FormerNames ?? @event?.FormerNames ?? [];
        var serializer = message?.Serializer ?? @event?.Serializer;
        return new MessagingContractDescriptor(contractType, @event is not null, owner, name, formerNames, serializer);
    }

    /// <summary>Normalizes a CLR type name to the portable lowercase snake_case form.</summary>
    public static string Normalize(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        var fullName = contractType.FullName ?? contractType.Name;
        return string.Join(".", fullName.Replace('+', '.').Split('.').Select(_normalizeSegment));
    }

    private static string _normalizeSegment(string segment)
    {
        var genericArity = segment.IndexOf('`', StringComparison.Ordinal);
        if (genericArity >= 0)
            segment = segment[..genericArity];
        var builder = new StringBuilder(segment.Length + 4);
        for (var i = 0; i < segment.Length; i++)
        {
            var current = segment[i];
            if (char.IsUpper(current)
                && i > 0
                && (char.IsLower(segment[i - 1])
                    || char.IsDigit(segment[i - 1])
                    || (char.IsUpper(segment[i - 1]) && i + 1 < segment.Length && char.IsLower(segment[i + 1]))))
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
