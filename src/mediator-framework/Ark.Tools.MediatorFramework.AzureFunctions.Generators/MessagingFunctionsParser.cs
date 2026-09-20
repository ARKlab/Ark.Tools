// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

using Ark.Tools.MediatorFramework.Generators;

using Microsoft.CodeAnalysis;

using static Ark.Tools.MediatorFramework.AzureFunctions.Generators.MessagingFunctionsNaming;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Generators;

/// <summary>A symbol-free description of a generated messaging subscription.</summary>
/// <param name="Topic">The logical topic name.</param>
/// <param name="Name">The logical subscription name.</param>
/// <param name="ForwardToQueue">The logical queue receiving forwarded messages.</param>
internal readonly record struct MessagingSubscriptionSpec(string Topic, string Name, string ForwardToQueue);

/// <summary>A symbol-free description of a messaging topic.</summary>
/// <param name="ContractTypeName">The fully qualified contract type.</param>
/// <param name="Name">The logical topic name.</param>
/// <param name="OwnerIdentity">The publishing participant identity.</param>
internal readonly record struct MessagingTopicSpec(string ContractTypeName, string Name, string OwnerIdentity);

/// <summary>A symbol-free description of an Azure Functions messaging host.</summary>
/// <param name="ParticipantTypeName">The fully qualified participant type.</param>
/// <param name="ParticipantDisplayName">The participant display name used by diagnostics.</param>
/// <param name="NetworkTypeName">The fully qualified network type.</param>
/// <param name="Binding">The trigger binding.</param>
/// <param name="Identity">The participant identity.</param>
/// <param name="Connection">The connection configuration key.</param>
/// <param name="ManagedIdentityConfigurationKey">The managed identity configuration key.</param>
/// <param name="RetryTypeName">The fully qualified retry policy type.</param>
/// <param name="IncomingSteps">The fully qualified incoming pipeline steps.</param>
/// <param name="OutgoingSteps">The fully qualified outgoing pipeline steps.</param>
/// <param name="StrictStorageQueueHostSettings">Whether Storage Queue host settings are enforced at runtime.</param>
/// <param name="Subscriptions">The generated subscriptions.</param>
/// <param name="DesiredTopics">The topics owned or consumed by the host.</param>
/// <param name="KnownTopics">Every topic declared by the network.</param>
/// <param name="HasIdentityQueue">Whether the participant consumes contracts.</param>
/// <param name="ResourceLifecycle">The network resource lifecycle.</param>
/// <param name="ResourceLifecycleText">The invariant resource lifecycle value.</param>
/// <param name="Location">The host attribute location.</param>
/// <param name="ParseDiagnostics">The diagnostics discovered before host.json validation.</param>
/// <param name="TopologyDiagnostics">The diagnostics discovered after host.json validation.</param>
/// <param name="ValidateHostJson">Whether Storage Queue host settings must be inspected.</param>
/// <param name="IsEmittable">Whether the host produces generated source.</param>
/// <param name="SenderOnly">Whether the participant only publishes.</param>
internal readonly record struct MessagingHostSpec(
    string ParticipantTypeName,
    string ParticipantDisplayName,
    string NetworkTypeName,
    int Binding,
    string Identity,
    string Connection,
    string? ManagedIdentityConfigurationKey,
    string? RetryTypeName,
    EquatableArray<string> IncomingSteps,
    EquatableArray<string> OutgoingSteps,
    bool StrictStorageQueueHostSettings,
    EquatableArray<MessagingSubscriptionSpec> Subscriptions,
    EquatableArray<MessagingTopicSpec> DesiredTopics,
    EquatableArray<MessagingTopicSpec> KnownTopics,
    bool HasIdentityQueue,
    int ResourceLifecycle,
    string ResourceLifecycleText,
    LocationSpec? Location,
    EquatableArray<DiagnosticSpec> ParseDiagnostics,
    EquatableArray<DiagnosticSpec> TopologyDiagnostics,
    bool ValidateHostJson,
    bool IsEmittable,
    bool SenderOnly);

/// <summary>Parses Azure Functions messaging hosts into symbol-free specifications.</summary>
internal static class MessagingFunctionsParser
{
    private const string _participantAttribute =
        "Ark.Tools.MediatorFramework.MessagingParticipantAttribute";
    private const string _networkAttribute =
        "Ark.Tools.MediatorFramework.MessagingNetworkAttribute";
    private const string _messageAttribute =
        "Ark.Tools.MediatorFramework.MessageAttribute";
    private const string _eventAttribute =
        "Ark.Tools.MediatorFramework.EventAttribute";
    private const string _apiGroupAttribute =
        "Ark.Tools.MediatorFramework.ApiGroupAttribute";
    private const int _serviceBusBinding = 0;
    private const int _storageQueueBinding = 1;

    /// <summary>Reads and analyzes every messaging host declared by an assembly attribute list.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The host specifications.</returns>
    public static ImmutableArray<MessagingHostSpec> _readHosts(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var hosts = ImmutableArray.CreateBuilder<MessagingHostSpec>();
        foreach (var attribute in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attribute.ConstructorArguments.Length < 2
                || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol participant
                || attribute.ConstructorArguments[1].Value is not int binding)
                continue;

            hosts.Add(_analyze(attribute, participant, binding, cancellationToken));
        }

        return hosts.ToImmutable();
    }

    private static MessagingHostSpec _analyze(
        AttributeData attribute,
        INamedTypeSymbol participant,
        int binding,
        CancellationToken cancellationToken)
    {
        var location = LocationSpec._from(attribute.ApplicationSyntaxReference);
        var participantDisplayName = participant.ToDisplayString();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticSpec>();
        var spec = new MessagingHostSpec(
            participant.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            participantDisplayName,
            string.Empty,
            binding,
            string.Empty,
            string.Empty,
            _string(attribute, "ManagedIdentityConfigurationKey"),
            null,
            _typeNames(_types(attribute, "IncomingSteps")),
            _typeNames(_types(attribute, "OutgoingSteps")),
            _bool(attribute, "StrictStorageQueueHostSettings"),
            EquatableArray<MessagingSubscriptionSpec>.Empty,
            EquatableArray<MessagingTopicSpec>.Empty,
            EquatableArray<MessagingTopicSpec>.Empty,
            false,
            0,
            "0",
            location,
            EquatableArray<DiagnosticSpec>.Empty,
            EquatableArray<DiagnosticSpec>.Empty,
            false,
            false,
            false);

        var participantAttribute = participant.GetAttributes().FirstOrDefault(static candidate =>
            candidate.AttributeClass?.ToDisplayString() == _participantAttribute);
        if (participantAttribute is null)
        {
            diagnostics.Add(_diagnostic(MessagingFunctionsDiagnostics._invalidParticipant, location, participantDisplayName));
            return spec with { ParseDiagnostics = diagnostics.ToImmutable() };
        }

        var networks = _findNetworks(participant, cancellationToken);
        if (networks.Count == 0)
        {
            diagnostics.Add(_diagnostic(MessagingFunctionsDiagnostics._missingNetwork, location, participantDisplayName));
            return spec with { ParseDiagnostics = diagnostics.ToImmutable() };
        }
        if (networks.Count != 1)
        {
            diagnostics.Add(_diagnostic(MessagingFunctionsDiagnostics._multipleNetworks, location, participantDisplayName));
            return spec with { ParseDiagnostics = diagnostics.ToImmutable() };
        }
        if (binding is not (_serviceBusBinding or _storageQueueBinding))
        {
            diagnostics.Add(_diagnostic(
                MessagingFunctionsDiagnostics._unsupportedBinding,
                location,
                binding.ToString(CultureInfo.InvariantCulture)));
            return spec with { ParseDiagnostics = diagnostics.ToImmutable() };
        }

        var (networkType, networkAttribute) = networks[0];
        var identity = _string(participantAttribute, "Identity") ?? _identity(participant);
        var processes = _types(participantAttribute, "Processes");
        var subscribes = _types(participantAttribute, "Subscribes");
        foreach (var contract in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MessagingContractTopologyValidator._validate(
                (descriptor, contractLocation, arguments) =>
                    diagnostics.Add(_diagnostic(descriptor, LocationSpec._from(contractLocation), arguments)),
                contract,
                participant,
                _int(participantAttribute, "DefaultSerializer"));
        }
        foreach (var member in _types(networkAttribute, "Members"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var memberAttribute = member.GetAttributes().FirstOrDefault(static candidate =>
                candidate.AttributeClass?.ToDisplayString() == _participantAttribute);
            if (memberAttribute is null)
                continue;
            foreach (var contract in _types(memberAttribute, "Publishes"))
                MessagingContractTopologyValidator._validate(
                    (descriptor, contractLocation, arguments) =>
                        diagnostics.Add(_diagnostic(descriptor, LocationSpec._from(contractLocation), arguments)),
                    contract,
                    member,
                    _int(memberAttribute, "DefaultSerializer"));
        }

        var retryType = _type(participantAttribute, "Retry");
        var hasConsumers = !processes.IsDefaultOrEmpty || !subscribes.IsDefaultOrEmpty;
        var validateHostJson = false;
        if (binding == _storageQueueBinding && hasConsumers)
        {
            if (retryType is null)
            {
                diagnostics.Add(_diagnostic(
                    MessagingFunctionsDiagnostics._missingStorageQueueRetry,
                    location,
                    participantDisplayName));
                return spec with { ParseDiagnostics = diagnostics.ToImmutable() };
            }

            validateHostJson = true;
        }

        var lifecycle = _int(networkAttribute, "ResourceLifecycle");
        spec = spec with
        {
            NetworkTypeName = networkType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Identity = identity,
            Connection = _string(attribute, "ConnectionConfigurationKey") ?? networkType.Name,
            RetryTypeName = retryType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            HasIdentityQueue = hasConsumers,
            SenderOnly = !hasConsumers,
            ResourceLifecycle = lifecycle,
            ResourceLifecycleText = lifecycle.ToString(CultureInfo.InvariantCulture),
            ParseDiagnostics = diagnostics.ToImmutable(),
            ValidateHostJson = validateHostJson,
        };

        var topology = ImmutableArray.CreateBuilder<DiagnosticSpec>();
        var subscriptions = _createSubscriptions(
            topology,
            networkType,
            networkAttribute,
            participant,
            participantAttribute,
            identity,
            subscribes,
            cancellationToken);
        if (subscriptions is null)
            return spec with { TopologyDiagnostics = topology.ToImmutable() };

        var topics = _createTopics(networkAttribute, cancellationToken);
        var subscribedNames = subscribes
            .Select(static contract => contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var desiredTopics = topics
            .Where(topic => string.Equals(topic.OwnerIdentity, identity, StringComparison.Ordinal)
                || subscribedNames.Contains(topic.ContractTypeName))
            .ToImmutableArray();
        var hasCollision = _validateNativeNames(topology, location, binding, identity, subscriptions.Value, topics);
        if (hasCollision)
            return spec with { TopologyDiagnostics = topology.ToImmutable() };

        if (!hasConsumers)
        {
            topology.Add(_diagnostic(
                MessagingFunctionsDiagnostics._senderOnly,
                location,
                participantDisplayName));
        }

        return spec with
        {
            Subscriptions = subscriptions.Value,
            DesiredTopics = desiredTopics,
            KnownTopics = topics,
            TopologyDiagnostics = topology.ToImmutable(),
            IsEmittable = true,
        };
    }

    private static List<(INamedTypeSymbol Type, AttributeData Attribute)> _findNetworks(
        INamedTypeSymbol participant,
        CancellationToken cancellationToken)
    {
        var networks = new List<(INamedTypeSymbol Type, AttributeData Attribute)>(1);
        foreach (var type in _allTypes(participant.ContainingAssembly.GlobalNamespace, cancellationToken))
        {
            var attribute = type.GetAttributes().FirstOrDefault(static candidate =>
                candidate.AttributeClass?.ToDisplayString() == _networkAttribute);
            if (attribute is null)
                continue;
            if (!_types(attribute, "Members").Any(member => SymbolEqualityComparer.Default.Equals(member, participant)))
                continue;

            networks.Add((type, attribute));

            // A participant may belong to exactly one network; a second match is enough to report the conflict.
            if (networks.Count == 2)
                break;
        }

        return networks;
    }

    private static EquatableArray<MessagingSubscriptionSpec>? _createSubscriptions(
        ImmutableArray<DiagnosticSpec>.Builder diagnostics,
        INamedTypeSymbol network,
        AttributeData networkAttribute,
        INamedTypeSymbol participant,
        AttributeData participantAttribute,
        string participantIdentity,
        ImmutableArray<INamedTypeSymbol> subscribedEvents,
        CancellationToken cancellationToken)
    {
        var members = _types(networkAttribute, "Members")
            .Select(static member => (Type: member, Attribute: member.GetAttributes().FirstOrDefault(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == _participantAttribute)))
            .Where(static item => item.Attribute is not null)
            .ToArray();
        var subscriptions = ImmutableArray.CreateBuilder<MessagingSubscriptionSpec>();
        var participantLocation = LocationSpec._from(participant);
        foreach (var subscribedEvent in subscribedEvents
            .OrderBy(static type => type.ToDisplayString(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publishers = members.Where(item =>
                    _types(item.Attribute, "Publishes").Any(contract =>
                        SymbolEqualityComparer.Default.Equals(contract, subscribedEvent)))
                .ToArray();
            if (publishers.Length != 1)
            {
                diagnostics.Add(_diagnostic(
                    MessagingFunctionsDiagnostics._invalidSubscription,
                    participantLocation,
                    subscribedEvent.ToDisplayString(),
                    network.ToDisplayString()));
                return null;
            }

            var supportedProtocols = _ints(participantAttribute, "Serializers");
            var publisherProtocol = _int(publishers[0].Attribute!, "DefaultSerializer");
            if (!supportedProtocols.Contains(publisherProtocol))
            {
                diagnostics.Add(_diagnostic(
                    MessagingFunctionsDiagnostics._serializerMismatch,
                    participantLocation,
                    participantIdentity,
                    publisherProtocol switch
                    {
                        1 => "MessagePack",
                        2 => "Protobuf",
                        _ => "Json",
                    },
                    _string(publishers[0].Attribute, "Identity") ?? publishers[0].Type.ToDisplayString(),
                    subscribedEvent.ToDisplayString()));
                return null;
            }

            var publisherIdentity = _string(publishers[0].Attribute, "Identity") ?? _identity(publishers[0].Type);
            var topic = publisherIdentity + "-" + _contractName(subscribedEvent);
            subscriptions.Add(new MessagingSubscriptionSpec(topic, participantIdentity, participantIdentity));
        }

        return subscriptions.ToImmutable();
    }

    private static EquatableArray<MessagingTopicSpec> _createTopics(
        AttributeData networkAttribute,
        CancellationToken cancellationToken)
    {
        var topics = ImmutableArray.CreateBuilder<MessagingTopicSpec>();
        foreach (var member in _types(networkAttribute, "Members"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var participant = member.GetAttributes().FirstOrDefault(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == _participantAttribute);
            if (participant is null)
                continue;
            var ownerIdentity = _string(participant, "Identity") ?? _identity(member);
            foreach (var contract in _types(participant, "Publishes"))
            {
                topics.Add(new MessagingTopicSpec(
                    contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    ownerIdentity + "-" + _contractName(contract),
                    ownerIdentity));
            }
        }

        return topics.ToImmutable();
    }

    private static bool _validateNativeNames(
        ImmutableArray<DiagnosticSpec>.Builder diagnostics,
        LocationSpec? location,
        int binding,
        string identity,
        EquatableArray<MessagingSubscriptionSpec> subscriptions,
        EquatableArray<MessagingTopicSpec> topics)
    {
        var nativeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var transportName = binding == _storageQueueBinding ? "Storage Queue" : "Service Bus";
        var hasCollision = false;
        var values = new List<string>
        {
            identity,
        };
        values.AddRange(topics.Values.Select(static topic => topic.Name));
        foreach (var subscription in subscriptions)
        {
            values.Add(subscription.Topic);
            values.Add(subscription.Name);
            values.Add(subscription.ForwardToQueue);
        }

        foreach (var logical in values.Distinct(StringComparer.Ordinal))
        {
            var native = _nativeName(logical, binding);
            if (nativeNames.TryGetValue(native, out var existing)
                && !string.Equals(existing, logical, StringComparison.Ordinal))
            {
                diagnostics.Add(_diagnostic(
                    MessagingFunctionsDiagnostics._nativeNameCollision,
                    location,
                    existing,
                    logical,
                    transportName,
                    native));
                hasCollision = true;
            }
            else
            {
                nativeNames[native] = logical;
            }
        }

        return hasCollision;
    }

    private static string _identity(INamedTypeSymbol participant)
    {
        return _normalizeIdentity(participant.Name.EndsWith("Participant", StringComparison.Ordinal)
            ? participant.Name.Substring(0, participant.Name.Length - "Participant".Length)
            : participant.Name);
    }

    private static string _contractName(INamedTypeSymbol contract)
    {
        var attribute = contract.GetAttributes().FirstOrDefault(static candidate =>
            candidate.AttributeClass?.ToDisplayString() is _messageAttribute or _eventAttribute);
        if (_string(attribute, "Name") is { } explicitName)
            return explicitName;
        var group = contract.GetAttributes()
            .FirstOrDefault(static candidate => candidate.AttributeClass?.ToDisplayString() == _apiGroupAttribute)
            ?.ConstructorArguments.FirstOrDefault().Value as string ?? "Ark";
        return _normalizeLogical(group) + "." + _normalizeLogical(contract.Name);
    }

    private static DiagnosticSpec _diagnostic(
        DiagnosticDescriptor descriptor,
        LocationSpec? location,
        params object[] arguments)
    {
        return new DiagnosticSpec(
            descriptor.Id,
            location,
            arguments.Select(static argument => argument?.ToString() ?? string.Empty).ToImmutableArray());
    }

    private static EquatableArray<string> _typeNames(ImmutableArray<INamedTypeSymbol> types)
        => types.Select(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray();

    private static IEnumerable<INamedTypeSymbol> _allTypes(INamespaceSymbol @namespace, CancellationToken cancellationToken)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return type;
            foreach (var nested in _nestedTypes(type))
                yield return nested;
        }

        foreach (var child in @namespace.GetNamespaceMembers())
        {
            foreach (var type in _allTypes(child, cancellationToken))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> _nestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var descendant in _nestedTypes(nested))
                yield return descendant;
        }
    }

    private static ImmutableArray<INamedTypeSymbol> _types(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return ImmutableArray<INamedTypeSymbol>.Empty;
        var value = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value;
        return value.Kind == TypedConstantKind.Array
            ? value.Values
                .Where(static item => item.Value is INamedTypeSymbol)
                .Select(static item => (INamedTypeSymbol)item.Value!)
                .ToImmutableArray()
            : ImmutableArray<INamedTypeSymbol>.Empty;
    }

    private static INamedTypeSymbol? _type(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value
            as INamedTypeSymbol;
    }

    private static bool _bool(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value
            is true;
    }

    private static int _int(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value
            is int value
            ? value
            : 0;
    }

    private static ImmutableArray<int> _ints(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return ImmutableArray<int>.Empty;
        var value = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value;
        return value.Kind == TypedConstantKind.Array
            ? value.Values
                .Where(static item => item.Value is int)
                .Select(static item => (int)item.Value!)
                .ToImmutableArray()
            : ImmutableArray<int>.Empty;
    }

    private static string? _string(AttributeData? attribute, string name)
    {
        return attribute?.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value
            as string;
    }
}
