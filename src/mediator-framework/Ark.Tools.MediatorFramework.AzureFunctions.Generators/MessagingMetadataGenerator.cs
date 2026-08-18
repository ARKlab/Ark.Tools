// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.AzureFunctions.Generators;

/// <summary>Validates transport-neutral messaging declarations and emits their metadata.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class MessagingMetadataGenerator : IIncrementalGenerator
{
    private const string _message = "Ark.MediatorFramework.MessageAttribute";
    private const string _event = "Ark.MediatorFramework.EventAttribute";
    private const string _network = "Ark.MediatorFramework.MessagingNetworkAttribute";
    private const string _participant = "Ark.MediatorFramework.MessagingParticipantAttribute";
    private static readonly DiagnosticDescriptor _missingOwner = _diagnostic(
        "ARKMF033", "Missing messaging owner", "Messaging contract '{0}' must declare a non-blank owner");
    private static readonly DiagnosticDescriptor _dualContract = _diagnostic(
        "ARKMF034", "Dual messaging contract", "Contract '{0}' cannot be both a message and an event");
    private static readonly DiagnosticDescriptor _unregisteredContract = _diagnostic(
        "ARKMF035", "Unregistered messaging contract", "Messaging contract '{0}' is not registered in a messaging network");
    private static readonly DiagnosticDescriptor _duplicateRegistration = _diagnostic(
        "ARKMF036", "Duplicate messaging registration", "Messaging network '{0}' registers contract '{1}' more than once");
    private static readonly DiagnosticDescriptor _missingParticipantNetwork = _diagnostic(
        "ARKMF037", "Missing participant network", "Messaging participant must reference a declared network profile");
    private static readonly DiagnosticDescriptor _duplicateParticipant = _diagnostic(
        "ARKMF038", "Duplicate messaging participant", "An assembly can declare only one MessagingParticipant");
    private static readonly DiagnosticDescriptor _invalidParticipant = _diagnostic(
        "ARKMF039", "Invalid messaging participant", "Messaging participant declaration is invalid: {0}");
    private static readonly DiagnosticDescriptor _invalidName = _diagnostic(
        "ARKMF040", "Invalid messaging name", "Messaging name '{0}' for contract '{1}' is not normalized lowercase snake_case");
    private static readonly DiagnosticDescriptor _invalidQueueName = _diagnostic(
        "ARKMF041", "Invalid messaging queue name", "Messaging owner or participant identity '{0}' must be a portable queue name");
    private static readonly DiagnosticDescriptor _reservedName = _diagnostic(
        "ARKMF042", "Reserved messaging name", "Messaging name '{0}' is reserved by the framework");
    private static readonly DiagnosticDescriptor _missingCapability = _diagnostic(
        "ARKMF043", "Missing messaging capability", "Messaging declaration '{0}' requires network capability '{1}'");
    private static readonly DiagnosticDescriptor _invalidEventContract = _diagnostic(
        "ARKMF044", "Invalid event contract", "Event contract '{0}' must implement Ark.Tools.Solid.ICommand");
    private static readonly DiagnosticDescriptor _duplicateName = _diagnostic(
        "ARKMF045", "Duplicate messaging name", "Messaging name or alias '{0}' is used by more than one contract");
    private static readonly DiagnosticDescriptor _aliasConflict = _diagnostic(
        "ARKMF046", "Messaging alias conflicts with a current name", "Messaging alias '{0}' conflicts with a current contract name");
    private static readonly DiagnosticDescriptor _topicTooLong = _diagnostic(
        "ARKMF047", "Messaging topic name too long", "Derived event topic '{0}' exceeds the Service Bus 260-character limit");
    private static readonly DiagnosticDescriptor _producerSubscription = _diagnostic(
        "ARKMF048", "Producer subscriptions are not allowed", "Producer participant '{0}' cannot declare event subscriptions");
    private static readonly DiagnosticDescriptor _invalidSerialization = _diagnostic(
        "ARKMF049", "Conflicting messaging serializer", "Contract '{0}' explicitly selects '{1}', which conflicts with network default '{2}'");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var messageTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                _message,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var eventTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                _event,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var networkTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                _network,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var participantAssemblies = context.SyntaxProvider.ForAttributeWithMetadataName(
                _participant,
                static (_, _) => true,
                static (attributeContext, _) => attributeContext.SemanticModel.Compilation.Assembly)
            .Collect();
        var sourceTypes = messageTypes.Combine(eventTypes).Combine(networkTypes)
            .Select(static (pair, _) =>
            {
                var ((messages, events), networks) = pair;
                return messages.AddRange(events).AddRange(networks)
                    .Distinct(SymbolEqualityComparer.Default)
                    .OfType<INamedTypeSymbol>()
                    .ToImmutableArray();
            });
        context.RegisterSourceOutput(sourceTypes.Combine(participantAssemblies),
            static (productionContext, input) => _emit(productionContext, input.Left, input.Right));
    }

    private static void _emit(
        SourceProductionContext context,
        ImmutableArray<INamedTypeSymbol> sourceTypes,
        ImmutableArray<IAssemblySymbol> participantAssemblies)
    {
        var participantAttributes = participantAssemblies.FirstOrDefault()?.GetAttributes()
            .Where(attribute => _is(attribute, _participant))
            .ToArray();
        participantAttributes ??= Array.Empty<AttributeData>();
        INamedTypeSymbol? participantNetwork = participantAttributes
            .Select(attribute => _typeArgument(attribute, "Network"))
            .FirstOrDefault(type => type is not null);
        var networkTypes = sourceTypes
            .Where(type => _hasAttribute(type, _network))
            .Concat(participantNetwork is null ? Array.Empty<INamedTypeSymbol>() : new[] { participantNetwork })
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .ToArray();
        var networks = networkTypes
            .Select(type => _readNetwork(type))
            .Where(network => network is not null)
            .Select(network => network!)
            .OrderBy(network => network.TypeName, StringComparer.Ordinal)
            .ToArray();
        var contractTypes = sourceTypes
            .Where(type => _hasAttribute(type, _message) || _hasAttribute(type, _event))
            .Concat(networks.SelectMany(network => network.Contracts))
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .ToArray();
        var contracts = contractTypes
            .Select(type => _readContract(type, context))
            .Where(contract => contract is not null)
            .Select(contract => contract!)
            .OrderBy(contract => contract.TypeName, StringComparer.Ordinal)
            .ToArray();

        _validateContracts(context, contracts, networks);
        if (participantAttributes.Length > 1)
            context.ReportDiagnostic(Diagnostic.Create(_duplicateParticipant, Location.None));

        ParticipantInfo? participant = null;
        if (participantAttributes.Length > 0)
            participant = _readParticipant(context, participantAttributes[0], networks);

        if (contracts.Length == 0 && networks.Length == 0 && participant is null)
            return;

        _emitMetadata(context, contracts, networks, participant);
    }

    private static void _validateContracts(
        SourceProductionContext context,
        ContractInfo[] contracts,
        NetworkInfo[] networks)
    {
        var registered = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var network in networks)
        {
            var names = new Dictionary<string, ContractInfo>(StringComparer.Ordinal);
            var currentNames = network.Contracts
                .Select(contractType => contracts.FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.Type, contractType)))
                .Where(contract => contract is not null)
                .Select(contract => contract!)
                .Select(contract => contract.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var contractType in network.Contracts)
            {
                var contract = contracts.FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.Type, contractType));
                if (contract is null)
                    continue;
                if (!registered.Add(contract.Type))
                {
                    // A contract may be shared by profiles; duplicate entries within one
                    // profile are still diagnosed below.
                }

                if (!names.TryAdd(contract.Name, contract))
                    context.ReportDiagnostic(Diagnostic.Create(
                        _duplicateRegistration, contract.Location, network.TypeName, contract.TypeName));
                foreach (var alias in contract.FormerNames)
                {
                    if (currentNames.Contains(alias))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            _aliasConflict, contract.Location, alias));
                        continue;
                    }
                    if (!names.TryAdd(alias, contract))
                        context.ReportDiagnostic(Diagnostic.Create(
                            _duplicateName, contract.Location, alias));
                }

                if (contract.IsEvent && !network.Requires.HasFlag(MessagingCapabilities.PubSub))
                    context.ReportDiagnostic(Diagnostic.Create(
                        _missingCapability, contract.Location, contract.TypeName, nameof(MessagingCapabilities.PubSub)));
                if (contract.Serializer is not null
                    && contract.Serializer.Value.ToString() != network.DefaultSerializer)
                    context.ReportDiagnostic(Diagnostic.Create(
                        _invalidSerialization, contract.Location, contract.TypeName,
                        contract.Serializer.Value, network.DefaultSerializer));

                if (contract.IsEvent)
                {
                    var topic = contract.Owner + "-" + contract.Name;
                    if (topic.Length > 260)
                        context.ReportDiagnostic(Diagnostic.Create(_topicTooLong, contract.Location, topic));
                }
            }
        }

        foreach (var contract in contracts)
        {
            if (!registered.Contains(contract.Type))
                context.ReportDiagnostic(Diagnostic.Create(
                    _unregisteredContract, contract.Location, contract.TypeName));
        }
    }

    private static ParticipantInfo? _readParticipant(
        SourceProductionContext context,
        AttributeData attribute,
        NetworkInfo[] networks)
    {
        var network = _typeArgument(attribute, "Network");
        if (network is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_missingParticipantNetwork, Location.None));
            return null;
        }

        var networkInfo = networks.FirstOrDefault(item =>
            SymbolEqualityComparer.Default.Equals(item.Type, network));
        if (networkInfo is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _invalidParticipant, Location.None, "the referenced network profile is not declared"));
            return null;
        }

        var identity = _stringArgument(attribute, "Identity");
        var role = _enumArgument(attribute, "Role");
        var subscriptions = _typeArguments(attribute, "Subscriptions");
        if (identity is not null)
        {
            _validateQueueName(context, identity, Location.None);
            if (identity == "outbox-processor")
                context.ReportDiagnostic(Diagnostic.Create(_reservedName, Location.None, identity));
        }

        if (subscriptions.Length > 0 && string.Equals(role, "Producer", StringComparison.Ordinal))
            context.ReportDiagnostic(Diagnostic.Create(
                _producerSubscription, Location.None, identity ?? "<unnamed>"));
        if (subscriptions.Length > 0 && identity is null)
            context.ReportDiagnostic(Diagnostic.Create(
                _invalidParticipant, Location.None, "subscriptions require an identity"));
        if (subscriptions.Length > 0 && !networkInfo.Requires.HasFlag(MessagingCapabilities.PubSub))
            context.ReportDiagnostic(Diagnostic.Create(
                _missingCapability, Location.None, "participant subscriptions", nameof(MessagingCapabilities.PubSub)));
        if (identity is not null
            && string.Equals(role, "Consumer", StringComparison.Ordinal)
            && !networkInfo.Requires.HasFlag(MessagingCapabilities.Receive))
            context.ReportDiagnostic(Diagnostic.Create(
                _missingCapability, Location.None, "participant identity", nameof(MessagingCapabilities.Receive)));

        foreach (var subscription in subscriptions)
        {
            var contract = networkInfo.Contracts.FirstOrDefault(item =>
                SymbolEqualityComparer.Default.Equals(item, subscription));
            if (contract is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _invalidParticipant, Location.None, "subscription is not registered in the referenced network"));
            }
            else if (!_hasAttribute(contract, _event))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _invalidParticipant, Location.None, "subscriptions must reference event contracts"));
            }
        }

        var received = networkInfo.Contracts
            .Where(contract => _hasAttribute(contract, _message)
                && identity is not null
                && string.Equals(_owner(contract, _message), identity, StringComparison.Ordinal))
            .OrderBy(contract => contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();
        return new ParticipantInfo(network, identity, role, subscriptions, _typeArguments(attribute, "IncomingSteps"),
            _typeArguments(attribute, "OutgoingSteps"), received);
    }

    private static ContractInfo? _readContract(INamedTypeSymbol type, SourceProductionContext context)
    {
        var message = type.GetAttributes().FirstOrDefault(attribute => _is(attribute, _message));
        var @event = type.GetAttributes().FirstOrDefault(attribute => _is(attribute, _event));
        if (message is null && @event is null)
            return null;
        if (message is not null && @event is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_dualContract, _location(type), type.Name));
            return null;
        }

        var attribute = message ?? @event!;
        var owner = _stringArgument(attribute, message is not null ? "OwnerQueue" : "OwnerPublisher");
        if (string.IsNullOrWhiteSpace(owner))
            context.ReportDiagnostic(Diagnostic.Create(_missingOwner, _location(type), type.Name));
        else
        {
            _validateQueueName(context, owner!, _location(type));
            if (owner == "outbox-processor" || (message is not null && owner!.EndsWith("-poison", StringComparison.Ordinal)))
                context.ReportDiagnostic(Diagnostic.Create(_reservedName, _location(type), owner!));
        }

        var explicitName = _stringArgument(attribute, "Name");
        var name = explicitName ?? _normalize(type);
        if (explicitName is not null && explicitName != _normalizeValue(explicitName))
            context.ReportDiagnostic(Diagnostic.Create(_invalidName, _location(type), explicitName, type.Name));
        var formerNames = _stringArguments(attribute, "FormerNames");
        foreach (var formerName in formerNames)
        {
            if (formerName != _normalizeValue(formerName))
                context.ReportDiagnostic(Diagnostic.Create(_invalidName, _location(type), formerName, type.Name));
        }

        if (@event is not null && !_implementsCommand(type))
            context.ReportDiagnostic(Diagnostic.Create(_invalidEventContract, _location(type), type.Name));

        return new ContractInfo(
            type,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            @event is not null,
            owner ?? string.Empty,
            name,
            formerNames,
            _serializerArgument(attribute),
            _location(type));
    }

    private static NetworkInfo? _readNetwork(INamedTypeSymbol type)
    {
        var attribute = type.GetAttributes().FirstOrDefault(item => _is(item, _network));
        if (attribute is null)
            return null;
        return new NetworkInfo(
            type,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            _typeArguments(attribute, "Contracts"),
            _enumFlags(attribute, "Requires"),
            _enumArgument(attribute, "DefaultSerializer"));
    }

    private static void _emitMetadata(
        SourceProductionContext context,
        ContractInfo[] contracts,
        NetworkInfo[] networks,
        ParticipantInfo? participant)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("namespace Ark.MediatorFramework.AzureFunctions.Generated;");
        source.AppendLine();
        source.AppendLine("public static class ArkGeneratedMessagingMetadata");
        source.AppendLine("{");
        source.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::Ark.MediatorFramework.Messaging.MessagingContractDescriptor> Contracts { get; } = new global::Ark.MediatorFramework.Messaging.MessagingContractDescriptor[]");
        source.AppendLine("    {");
        foreach (var contract in contracts.Where(contract => networks.Any(network =>
            network.Contracts.Any(type => SymbolEqualityComparer.Default.Equals(type, contract.Type))))
            .OrderBy(contract => contract.Name, StringComparer.Ordinal))
        {
            source.Append("        new global::Ark.MediatorFramework.Messaging.MessagingContractDescriptor(typeof(")
                .Append(contract.TypeName).Append("), ")
                .Append(contract.IsEvent ? "true" : "false").Append(", ")
                .Append(_literal(contract.Owner)).Append(", ")
                .Append(_literal(contract.Name)).Append(", new string[] { ")
                .Append(string.Join(", ", contract.FormerNames.Select(_literal))).Append(" }, ")
                .Append(contract.Serializer is null ? "null" : "global::Ark.MediatorFramework.SerializationProtocol." + contract.Serializer)
                .AppendLine("),");
        }
        source.AppendLine("    };");
        source.AppendLine();
        source.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::System.Type> Networks { get; } = new global::System.Type[]");
        source.AppendLine("    {");
        foreach (var network in networks)
            source.Append("        typeof(").Append(network.TypeName).AppendLine("),");
        source.AppendLine("    };");
        if (participant is not null)
        {
            source.AppendLine();
            source.AppendLine("    public static global::Ark.MediatorFramework.Messaging.MessagingParticipantDescriptor Participant { get; } = new(");
            source.Append("        typeof(").Append(participant.Network.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("), ")
                .Append(participant.Identity is null ? "null" : _literal(participant.Identity)).Append(", ")
                .Append("global::Ark.MediatorFramework.MessagingParticipantRole.").Append(participant.Role).AppendLine(",");
            _emitTypes(source, participant.Subscriptions);
            source.AppendLine(",");
            _emitTypes(source, participant.IncomingSteps);
            source.AppendLine(",");
            _emitTypes(source, participant.OutgoingSteps);
            source.AppendLine(",");
            _emitTypes(source, participant.ReceivedMessages);
            source.AppendLine("    );");
        }
        source.AppendLine("}");
        context.AddSource("ArkGeneratedMessagingMetadata.g.cs", source.ToString());
    }

    private static void _emitTypes(StringBuilder source, ImmutableArray<INamedTypeSymbol> types)
    {
        source.Append("        new global::System.Type[] { ")
            .Append(string.Join(", ", types.Select(type => "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
            .Append(" }");
    }

    private static bool _implementsCommand(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(item =>
            item.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Ark.Tools.Solid.ICommand"
            || item.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::Ark.Tools.Solid.ICommand<TSelf>");
    }

    private static void _validateQueueName(SourceProductionContext context, string value, Location location)
    {
        if (value.Length < 3 || value.Length > 63 || value.Contains("--", StringComparison.Ordinal)
            || !char.IsLetterOrDigit(value[0]) || !char.IsLetterOrDigit(value[^1])
            || value.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            context.ReportDiagnostic(Diagnostic.Create(_invalidQueueName, location, value));
    }

    private static string _normalize(INamedTypeSymbol type)
    {
        return _normalizeValue(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace("+", "."));
    }

    private static string _normalizeValue(string value)
    {
        return string.Join(".", value.Split('.').Select(_normalizeSegment));
    }

    private static string _normalizeSegment(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current)
                && index > 0
                && (char.IsLower(value[index - 1])
                    || char.IsDigit(value[index - 1])
                    || (char.IsUpper(value[index - 1]) && index + 1 < value.Length && char.IsLower(value[index + 1]))))
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(current));
        }
        return builder.ToString();
    }

    private static DiagnosticDescriptor _diagnostic(string id, string title, string message) =>
        new(id, title, message, "Ark.MediatorFramework", DiagnosticSeverity.Error, true);

    private static bool _is(AttributeData attribute, string name) =>
        attribute.AttributeClass?.ToDisplayString() == name;

    private static bool _hasAttribute(ITypeSymbol type, string name) =>
        type.GetAttributes().Any(attribute => _is(attribute, name));

    private static string? _owner(ITypeSymbol type, string attributeName)
    {
        var attribute = type.GetAttributes().FirstOrDefault(item => _is(item, attributeName));
        return attribute is null ? null : _stringArgument(attribute, attributeName == _message ? "OwnerQueue" : "OwnerPublisher");
    }

    private static string? _stringArgument(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value.Value as string;

    private static ImmutableArray<string> _stringArguments(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value.Kind == TypedConstantKind.Array
            ? value.Values.Where(item => item.Value is string).Select(item => (string)item.Value!).ToImmutableArray()
            : ImmutableArray<string>.Empty;
    }

    private static ImmutableArray<INamedTypeSymbol> _typeArguments(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value.Kind == TypedConstantKind.Array
            ? value.Values.Where(item => item.Kind == TypedConstantKind.Type && item.Value is INamedTypeSymbol)
                .Select(item => (INamedTypeSymbol)item.Value!).ToImmutableArray()
            : ImmutableArray<INamedTypeSymbol>.Empty;
    }

    private static INamedTypeSymbol? _typeArgument(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value.Value as INamedTypeSymbol;

    private static string _enumArgument(AttributeData attribute, string name)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        if (argument.Value is not int value)
            return name == "DefaultSerializer" ? nameof(SerializationProtocol.Json) : "Consumer";
        if (name == "Role")
            return value == (int)MessagingParticipantRole.Producer
                ? nameof(MessagingParticipantRole.Producer)
                : nameof(MessagingParticipantRole.Consumer);
        return value switch
        {
            (int)SerializationProtocol.MessagePack => nameof(SerializationProtocol.MessagePack),
            (int)SerializationProtocol.Protobuf => nameof(SerializationProtocol.Protobuf),
            _ => nameof(SerializationProtocol.Json),
        };
    }

    private static MessagingCapabilities _enumFlags(AttributeData attribute, string name)
    {
        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int integer)
            return (MessagingCapabilities)integer;
        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value.Value;
        return value is int namedInteger ? (MessagingCapabilities)namedInteger : MessagingCapabilities.None;
    }

    private static SerializationProtocol? _serializerArgument(AttributeData attribute)
    {
        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == "Serializer").Value;
        return value.Value is int integer ? (SerializationProtocol)integer : null;
    }

    private static Location _location(INamedTypeSymbol type) => type.Locations.FirstOrDefault() ?? Location.None;

    private static string _literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private sealed class ContractInfo
    {
        public ContractInfo(
            INamedTypeSymbol type,
            string typeName,
            bool isEvent,
            string owner,
            string name,
            ImmutableArray<string> formerNames,
            SerializationProtocol? serializer,
            Location location)
        {
            Type = type;
            TypeName = typeName;
            IsEvent = isEvent;
            Owner = owner;
            Name = name;
            FormerNames = formerNames;
            Serializer = serializer;
            Location = location;
        }

        public INamedTypeSymbol Type { get; }
        public string TypeName { get; }
        public bool IsEvent { get; }
        public string Owner { get; }
        public string Name { get; }
        public ImmutableArray<string> FormerNames { get; }
        public SerializationProtocol? Serializer { get; }
        public Location Location { get; }
    }

    private sealed class NetworkInfo
    {
        public NetworkInfo(
            INamedTypeSymbol type,
            string typeName,
            ImmutableArray<INamedTypeSymbol> contracts,
            MessagingCapabilities requires,
            string defaultSerializer)
        {
            Type = type;
            TypeName = typeName;
            Contracts = contracts;
            Requires = requires;
            DefaultSerializer = defaultSerializer;
        }

        public INamedTypeSymbol Type { get; }
        public string TypeName { get; }
        public ImmutableArray<INamedTypeSymbol> Contracts { get; }
        public MessagingCapabilities Requires { get; }
        public string DefaultSerializer { get; }
    }

    private sealed class ParticipantInfo
    {
        public ParticipantInfo(
            INamedTypeSymbol network,
            string? identity,
            string role,
            ImmutableArray<INamedTypeSymbol> subscriptions,
            ImmutableArray<INamedTypeSymbol> incomingSteps,
            ImmutableArray<INamedTypeSymbol> outgoingSteps,
            INamedTypeSymbol[] receivedMessages)
        {
            Network = network;
            Identity = identity;
            Role = role;
            Subscriptions = subscriptions;
            IncomingSteps = incomingSteps;
            OutgoingSteps = outgoingSteps;
            ReceivedMessages = receivedMessages.ToImmutableArray();
        }

        public INamedTypeSymbol Network { get; }
        public string? Identity { get; }
        public string Role { get; }
        public ImmutableArray<INamedTypeSymbol> Subscriptions { get; }
        public ImmutableArray<INamedTypeSymbol> IncomingSteps { get; }
        public ImmutableArray<INamedTypeSymbol> OutgoingSteps { get; }
        public ImmutableArray<INamedTypeSymbol> ReceivedMessages { get; }
    }

    [Flags]
    private enum MessagingCapabilities
    {
        None = 0,
        Receive = 1,
        PubSub = 2,
        ScheduledSend = 4,
    }

    private enum SerializationProtocol
    {
        Json,
        MessagePack,
        Protobuf,
    }

    private enum MessagingParticipantRole
    {
        Consumer,
        Producer,
    }
}
