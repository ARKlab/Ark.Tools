// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>Validates messaging topology and emits deterministic network metadata.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class MessagingNetworkGenerator : IIncrementalGenerator
{
    private const string _networkAttribute = "Ark.Tools.MediatorFramework.MessagingNetworkAttribute";
    private const string _participantAttribute = "Ark.Tools.MediatorFramework.MessagingParticipantAttribute";
    private const string _messageAttribute = "Ark.Tools.MediatorFramework.MessageAttribute";
    private const string _eventAttribute = "Ark.Tools.MediatorFramework.EventAttribute";
    private const string _apiGroupAttribute = "Ark.Tools.MediatorFramework.ApiGroupAttribute";
    private const string _requestNamespace = "Ark.Tools.Solid";
    private const string _commandInterface = "Ark.Tools.Solid.ICommand`1";
    private const string _commandInterfaceName = "ICommand`1";
    private const string _requestInterfaceName = "IRequest`2";
    private const string _payloadReader = "Ark.Tools.MediatorFramework.Messaging.IMessagingPayloadReader";
    private const string _streamPayloadReader = "Ark.Tools.MediatorFramework.Messaging.MessagingStreamPayloadReader";
    private const string _codec = "Ark.Tools.MediatorFramework.Messaging.IMessagingCodec";
    private const string _payloadSender = "Ark.Tools.MediatorFramework.Messaging.MessagingPayloadSender";
    private const string _dataBus = "Ark.Tools.MediatorFramework.IMessagingDataBus";
    private const string _networkOptions = "Ark.Tools.MediatorFramework.Messaging.MessagingNetworkOptions";
    private const string _participantDescriptor = "Ark.Tools.MediatorFramework.Messaging.MessagingParticipantDescriptor";
    private const string _contractRegistry = "Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry";
    private const string _commandProcessor = "Ark.Tools.Solid.ICommandProcessor";
    private const string _requestProcessor = "Ark.Tools.Solid.IRequestProcessor";
    private const string _failFastException = "Ark.Tools.MediatorFramework.Messaging.MessagingFailFastException";
    private const string _failFastReason = "Ark.Tools.MediatorFramework.Messaging.MessagingFailFastReason";
    private const string _failedMessage = "Ark.Tools.MediatorFramework.MessagingFailed`1";
    private const string _exceptionInfo = "Ark.Tools.MediatorFramework.MessagingExceptionInfo";
    private const string _specStage = "MessagingNetworkSpecs";
    private const string _outputStage = "MessagingNetworkOutput";
    private const string _networkStage = "MessagingNetworkParser";
    private const string _participantStage = "MessagingParticipantParser";
    private const int _sendReceive = 1;
    private const int _pubSub = 2;

    private static readonly DiagnosticDescriptor _duplicateMember = _rule(
        "ARKMSG001", "Duplicate messaging network member",
        "Network '{0}' lists participant '{1}' more than once", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _missingParticipant = _rule(
        "ARKMSG002", "Messaging network member is not a participant",
        "Network '{0}' lists '{1}', which is not marked with MessagingParticipant", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _dualContract = _rule(
        "ARKMSG003", "Contract has multiple messaging kinds",
        "Contract '{0}' cannot be both a message and an event", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _multipleNetworks = _rule(
        "ARKMSG004", "Participant belongs to multiple networks",
        "Participant '{0}' is listed by more than one messaging network", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _multipleProcessor = _rule(
        "ARKMSG005", "Message has multiple processors",
        "Message '{0}' is processed by more than one participant in network '{1}'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _multiplePublisher = _rule(
        "ARKMSG006", "Event has multiple publishers",
        "Event '{0}' is published by more than one participant in network '{1}'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _unwiredContract = _rule(
        "ARKMSG007", "Messaging contract is unwired",
        "Contract '{0}' is not owned by a participant in network '{1}'", DiagnosticSeverity.Info);
    private static readonly DiagnosticDescriptor _unsatisfiableSubscription = _rule(
        "ARKMSG008", "Messaging subscription cannot be satisfied",
        "Participant '{0}' subscribes to event '{1}', which is not published in network '{2}'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _serializerMismatch = _rule(
        "ARKMSG009", "Subscriber cannot deserialize publisher protocol",
        "Participant '{0}' does not support effective protocol '{3}' of publisher '{1}' for event '{2}'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _defaultSerializer = _rule(
        "ARKMSG010", "Default serializer is not supported",
        "Participant '{0}' has DefaultSerializer '{1}' outside its Serializers set", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _missingCapability = _rule(
        "ARKMSG011", "Messaging capability is not declared",
        "Participant '{0}' requires capability '{1}', but network '{2}' does not declare it", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _crossNetworkContract = _rule(
        "ARKMSG012", "Contract belongs to multiple networks",
        "Contract '{0}' is declared by participants in more than one messaging network", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidIdentity = _rule(
        "ARKMSG013", "Invalid participant identity",
        "Participant '{0}' has identity '{1}', which is not a valid logical name", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _duplicateIdentity = _rule(
        "ARKMSG014", "Duplicate participant identity",
        "Network '{0}' contains more than one participant with identity '{1}'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _reservedIdentity = _rule(
        "ARKMSG015", "Reserved participant identity",
        "Participant '{0}' uses reserved identity '{1}'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidRetry = _rule(
        "ARKMSG017", "Invalid messaging retry policy",
        "Retry policy for participant '{0}' must have MaximumDeliveryCount >= {1}", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidEventShape = _rule(
        "ARKMSG018", "Invalid event contract",
        "Event contract '{0}' must implement ICommand<TSelf> (requests and queries cannot be events)", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _undispatchableMessage = _rule(
        "ARKMSG027", "Processed contract cannot be dispatched",
        "Participant '{0}' processes '{1}', which implements neither ICommand<TSelf> nor IRequest<TSelf, TResponse>", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _duplicateParticipantContract = _rule(
        "ARKMSG028", "Participant lists a contract more than once",
        "Participant '{0}' lists '{1}' more than once in {2}", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _nonNormalizedName = _rule(
        "ARKMSG019", "Non-normalized contract name",
        "Contract '{0}' has explicit name or alias '{1}', which is not a valid logical name", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _duplicateName = _rule(
        "ARKMSG020", "Duplicate messaging contract name",
        "Messaging contract name '{0}' is used by more than one contract", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _duplicateAlias = _rule(
        "ARKMSG021", "Duplicate messaging contract alias",
        "Messaging contract alias '{0}' is declared more than once", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _aliasCollision = _rule(
        "ARKMSG022", "Messaging contract alias collision",
        "Messaging contract alias '{0}' collides with a current contract name", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _nonPartialDeclaringType = _rule(
        "ARKMSG023", "Messaging declaring type must be partial",
        "Type '{0}' is marked with [{1}] but is not a non-nested, non-generic partial class, so its routing members cannot be generated",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _versionOnlyDefaultName = _rule(
        "ARKMSG029", "Default messaging contract name is version-only",
        "Contract '{0}' has no explicit name and its default logical name '{1}' derives from a bare version type name; set an explicit name, for example [{2}(Name = \"...\")]",
        DiagnosticSeverity.Warning);
    private static DiagnosticDescriptor _rule(string id, string title, string message, DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(id, title, message, "Ark.Tools.MediatorFramework", severity, true,
            helpLinkUri: $"https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/{id}.md");
    }

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Symbols are read inside the transforms and projected into equatable specs, so validation and
        // emission rerun only when a network, participant, contract or referenced runtime type changes.
        var networks = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                _networkAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => attributeContext.TargetSymbol is INamedTypeSymbol symbol
                    ? _readNetwork(symbol, attributeContext.SemanticModel.Compilation)
                    : (Network?)null)
            .WithTrackingName(_networkStage)
            .Where(static network => network is not null)
            .Select(static (network, _) => network!.Value)
            .Collect()
            .Select(static (values, _) => new EquatableArray<Network>(values));
        var participants = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                _participantAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => attributeContext.TargetSymbol is INamedTypeSymbol symbol
                    ? _readParticipant(symbol, attributeContext.SemanticModel.Compilation)
                    : null)
            .WithTrackingName(_participantStage)
            .Where(static participant => participant is not null)
            .Select(static (participant, _) => participant!.Value)
            .Collect()
            .Select(static (values, _) => new EquatableArray<Participant>(values));
        var compilationFacts = context.CompilationProvider
            .Select(static (compilation, _) => _readCompilationFacts(compilation));

        var specs = networks.Combine(participants).Combine(compilationFacts)
            .Select(static (input, cancellationToken) =>
            {
                var ((networkSpecs, participantSpecs), facts) = input;
                var sink = new GenerationSink(cancellationToken);
                _emit(
                    sink,
                    networkSpecs.Distinct(),
                    participantSpecs.Distinct(),
                    facts);
                return sink._toSpec();
            })
            .WithTrackingName(_specStage);
        var output = specs
            .Select(static (spec, _) => spec)
            .WithTrackingName(_outputStage);

        context.RegisterSourceOutput(output, static (productionContext, spec) =>
        {
            foreach (var diagnostic in spec.Diagnostics)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    _descriptor(diagnostic.DescriptorId),
                    LocationSpec._toLocation(diagnostic.Location),
                    diagnostic.Arguments.Values.Cast<object?>().ToArray()));
            }
            foreach (var source in spec.Sources)
                productionContext.AddSource(source.HintName, source.Source);
        });
    }

    private static void _emit(
        GenerationSink context,
        IEnumerable<Network> networkSpecs,
        IEnumerable<Participant> participantSpecs,
        CompilationFacts facts)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var networks = networkSpecs
            .OrderBy(static network => network.Symbol.DisplayName, StringComparer.Ordinal)
            .ToArray();
        var participantNetworks = new Dictionary<TypeSpec, List<Network>>();
        var allContracts = new Dictionary<ContractSpec, HashSet<TypeSpec>>();
        // A participant belonging to several networks is reported; its descriptor uses the first one.
        var networkMembers = new Dictionary<TypeSpec, List<Participant>>();

        foreach (var network in networks)
        {
            var seen = new HashSet<TypeSpec>();
            var participants = new List<Participant>();
            foreach (var member in network.Members)
            {
                if (!seen.Add(member.Symbol))
                {
                    _report(context, _duplicateMember, network.Symbol, network.Name, member.Symbol.DisplayName);
                    continue;
                }

                var participant = member.Participant;
                if (participant is null)
                {
                    _report(context, _missingParticipant, network.Symbol, network.Name, member.Symbol.DisplayName);
                    continue;
                }

                participants.Add(participant.Value);
                if (!participantNetworks.TryGetValue(member.Symbol, out var memberships))
                    participantNetworks.Add(member.Symbol, memberships = new List<Network>());
                memberships.Add(network);
                foreach (var contract in participant.Value.Contracts)
                {
                    if (!allContracts.TryGetValue(contract, out var declarations))
                        allContracts.Add(contract, declarations = new HashSet<TypeSpec>());
                    declarations.Add(network.Symbol);
                }
            }

            _validateNetwork(context, network, participants);
            foreach (var participant in participants)
            {
                if (!networkMembers.ContainsKey(participant.Symbol))
                    networkMembers.Add(participant.Symbol, participants);
            }
        }

        foreach (var membership in participantNetworks.Where(static pair => pair.Value.Count > 1))
            _report(context, _multipleNetworks, membership.Key, membership.Key.DisplayName);
        foreach (var contract in allContracts.Where(static pair => pair.Value.Count > 1))
            _report(context, _crossNetworkContract, contract.Key, contract.Key.DisplayName);

        _validateContractNames(context, allContracts.Keys);
        _emitMetadata(context, networks);
        foreach (var network in networks)
            _emitNetwork(context, network, facts);

        foreach (var participant in participantSpecs
            .OrderBy(static participant => participant.Symbol.DisplayName, StringComparer.Ordinal))
        {
            _emitParticipant(
                context,
                participant,
                networkMembers.TryGetValue(participant.Symbol, out var members) ? members : new List<Participant>(),
                facts);
        }
    }

    private static void _validateNetwork(
        GenerationSink context,
        Network network,
        IReadOnlyList<Participant> participants)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var processors = new Dictionary<ContractSpec, List<Participant>>();
        var publishers = new Dictionary<ContractSpec, List<Participant>>();

        foreach (var participant in participants)
        {
            if (!identities.Add(participant.Identity))
                _report(context, _duplicateIdentity, participant.Symbol, network.Name, participant.Identity);
            if (!_isLogicalName(participant.Identity))
                _report(context, _invalidIdentity, participant.Symbol, participant.Symbol.DisplayName, participant.Identity);
            if (participant.Identity == "outbox-processor" || participant.Identity.EndsWith("-poison", StringComparison.Ordinal))
                _report(context, _reservedIdentity, participant.Symbol, participant.Symbol.DisplayName, participant.Identity);
            if (participant.Contracts.Count > 0 && !participant.Serializers.Contains(participant.DefaultSerializer))
                _report(context, _defaultSerializer, participant.Symbol, participant.Identity, participant.DefaultSerializer);
            if (participant.Retry is not null
                && participant.Retry.Value.MaximumDeliveryCount < (participant.Retry.Value.SecondLevelRetriesEnabled ? 2 : 1))
                _report(context, _invalidRetry, participant.Symbol, participant.Identity, participant.Retry.Value.SecondLevelRetriesEnabled ? 2 : 1);

            if (participant.Processes.Count > 0 || participant.Subscribes.Count > 0)
                _requireCapability(context, network, participant, "SendReceive", _sendReceive);
            if (participant.Publishes.Count > 0 || participant.Subscribes.Count > 0)
                _requireCapability(context, network, participant, "PubSub", _pubSub);

            _reportDuplicates(context, participant, participant.Processes, "Processes");
            _reportDuplicates(context, participant, participant.Publishes, "Publishes");
            _reportDuplicates(context, participant, participant.Subscribes, "Subscribes");

            // Distinct, so a duplicate entry is reported once as ARKMSG028 rather than as a second owner.
            foreach (var contract in participant.Processes.Distinct())
            {
                _add(processors, contract, participant);
                if (!contract.IsSelfCommand && contract.RequestResponseTypeName is null)
                    _report(context, _undispatchableMessage, participant.Symbol, participant.Identity, contract.DisplayName);
            }
            foreach (var contract in participant.Publishes.Distinct())
                _add(publishers, contract, participant);
        }

        foreach (var processor in processors)
            _validateTopology(context, processor.Key, processor.Value[0].Symbol, processor.Value[0].DefaultSerializer);
        foreach (var publisher in publishers)
            _validateTopology(context, publisher.Key, publisher.Value[0].Symbol, publisher.Value[0].DefaultSerializer);

        foreach (var processor in processors)
        {
            if (processor.Value.Count > 1)
                _report(context, _multipleProcessor, processor.Key, processor.Key.ContractName, network.Name);
        }
        foreach (var publisher in publishers)
        {
            if (publisher.Value.Count > 1)
                _report(context, _multiplePublisher, publisher.Key, publisher.Key.ContractName, network.Name);
        }

        foreach (var participant in participants)
        {
            foreach (var subscription in participant.Subscribes)
            {
                if (!publishers.TryGetValue(subscription, out var eventPublishers))
                {
                    _report(context, _unsatisfiableSubscription, participant.Symbol, participant.Identity, subscription.ContractName, network.Name);
                    continue;
                }
                if (eventPublishers.Count != 1)
                    continue;
                if (!participant.Serializers.Contains(eventPublishers[0].DefaultSerializer))
                    _report(
                        context,
                        _serializerMismatch,
                        participant.Symbol,
                        participant.Identity,
                        eventPublishers[0].Identity,
                        subscription.ContractName,
                        _protocolName(eventPublishers[0].DefaultSerializer));
            }
        }

        foreach (var contract in participants.SelectMany(static participant => participant.Contracts)
            .Distinct())
        {
            var hasProcessor = processors.ContainsKey(contract);
            var hasPublisher = publishers.ContainsKey(contract);
            if (contract.HasMessage && contract.HasEvent)
                _report(context, _dualContract, contract, contract.DisplayName);
            // An event (declared, published, or subscribed) must be a command; a request is a message, never an event.
            var isEvent = contract.HasEvent
                || hasPublisher
                || participants.Any(participant => participant.Subscribes.Contains(contract));
            if (isEvent && !contract.IsSelfCommand)
                _report(context, _invalidEventShape, contract, contract.DisplayName);
            if (!hasProcessor && !hasPublisher)
                _report(context, _unwiredContract, contract, contract.ContractName, network.Name);
        }
    }

    private static void _validateTopology(
        GenerationSink context,
        ContractSpec contract,
        TypeSpec owner,
        int protocol)
    {
        var descriptor = MessagingContractTopologyValidator._missingShape(
            protocol,
            contract.HasMessagePackAttribute,
            contract.HasGoogleProtobufShape);
        if (descriptor is not null)
            context._report(descriptor, contract.Location, contract.DisplayName, owner.DisplayName);
    }

    private static void _requireCapability(
        GenerationSink context,
        Network network,
        Participant participant,
        string name,
        int capability)
    {
        if ((network.Requires & capability) == 0)
            _report(context, _missingCapability, participant.Symbol, participant.Identity, name, network.Name);
    }

    private static void _validateContractNames(
        GenerationSink context,
        IEnumerable<ContractSpec> contracts)
    {
        var currentNames = new Dictionary<string, ContractSpec>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, ContractSpec>(StringComparer.Ordinal);
        foreach (var contract in contracts.OrderBy(static contract => contract.DisplayName, StringComparer.Ordinal))
        {
            var current = contract.HasMessage || contract.HasEvent
                ? contract.ExplicitName ?? contract.DefaultContractName
                : contract.ContractName;
            if (contract.ExplicitName is not null && !_isNormalized(contract.ExplicitName))
                _report(context, _nonNormalizedName, contract, contract.DisplayName, contract.ExplicitName);
            foreach (var alias in contract.FormerNames)
            {
                if (!_isNormalized(alias))
                    _report(context, _nonNormalizedName, contract, contract.DisplayName, alias);
                if (!aliases.TryAdd(alias, contract))
                    _report(context, _duplicateAlias, contract, alias);
            }
            if (contract.HasVersionOnlyDefaultName)
                _report(context, _versionOnlyDefaultName, contract, contract.DisplayName, current, contract.HasMessage ? "Message" : "Event");
            if (!currentNames.TryAdd(current, contract))
                _report(context, _duplicateName, contract, current);
        }

        foreach (var alias in aliases)
        {
            if (currentNames.ContainsKey(alias.Key))
                _report(context, _aliasCollision, alias.Value, alias.Key);
        }
    }

    private static Network _readNetwork(INamedTypeSymbol symbol, Compilation compilation)
    {
        var attribute = symbol.GetAttributes().First(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == _networkAttribute);
        var members = _types(attribute, "Members");
        return new Network(
            _readType(symbol),
            symbol.ToDisplayString(),
            members
                .Select(member => new NetworkMember(_readType(member), _readParticipant(member, compilation)))
                .ToImmutableArray(),
            _enum(attribute, "Requires"),
            _optionalInt(attribute, "MaximumDecompressedPayloadBytes"),
            _optionalInt(attribute, "DataBusMaximumAttachmentBytes"),
            _optionalInt(attribute, "MaximumSchedulingDelaySeconds"),
            _optionalInt(attribute, "ResourceLifecycle"));
    }

    private static Participant? _readParticipant(INamedTypeSymbol symbol, Compilation compilation)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == _participantAttribute);
        if (attribute is null)
            return null;

        var explicitIdentity = _string(attribute, "Identity");
        var identity = explicitIdentity ?? _normalizeIdentity(symbol.Name.EndsWith("Participant", StringComparison.Ordinal)
            ? symbol.Name.Substring(0, symbol.Name.Length - "Participant".Length)
            : symbol.Name);
        var serializers = _enums(attribute, "Serializers");
        var compression = _enum(attribute, "Compression");
        var compressionMinimumSizeBytes = _int(attribute, "CompressionMinimumSizeBytes");
        var retryType = _type(attribute, "Retry");
        var retry = retryType is null ? null : _readRetry(retryType);
        var processes = _types(attribute, "Processes");
        var publishes = _types(attribute, "Publishes");
        var subscribes = _types(attribute, "Subscribes");
        return new Participant(
            _readType(symbol),
            identity,
            _readContracts(processes),
            _readContracts(publishes),
            _readContracts(subscribes),
            serializers,
            _enum(attribute, "DefaultSerializer"),
            compression,
            _compressionName(compilation, compression),
            compressionMinimumSizeBytes,
            retryType is null ? null : _typeName(retryType),
            retry,
            _readContracts(
                processes.Concat(publishes).Concat(subscribes)
                    .Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>()));
    }

    private static TypeSpec _readType(INamedTypeSymbol symbol)
    {
        return new TypeSpec(
            (symbol.ContainingAssembly?.Name ?? string.Empty) + "|" + _typeName(symbol),
            symbol.ToDisplayString(),
            _typeName(symbol),
            symbol.Name,
            symbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
                ? containingNamespace.ToDisplayString()
                : null,
            _accessibility(symbol),
            _isValidDeclaringType(symbol),
            LocationSpec._from(symbol.Locations.FirstOrDefault()));
    }

    private static EquatableArray<ContractSpec> _readContracts(IEnumerable<INamedTypeSymbol> contracts)
    {
        return contracts
            .Select(static contract =>
            {
                var attributes = _contractAttributes(contract);
                var request = _selfInterface(contract, _requestInterfaceName);
                return new ContractSpec(
                    _readType(contract),
                    _contractName(contract),
                    _defaultContractName(contract),
                    attributes.Message is not null,
                    attributes.Event is not null,
                    attributes.Name,
                    attributes.FormerNames,
                    attributes.Name is null
                        && (attributes.Message is not null || attributes.Event is not null)
                        && _isBareVersion(contract.Name),
                    _selfInterface(contract, _commandInterfaceName) is not null,
                    request?.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    MessagingContractTopologyValidator._hasMessagePackAttribute(contract),
                    MessagingContractTopologyValidator._hasGoogleProtobufShape(contract));
            })
            .ToImmutableArray();
    }

    private static CompilationFacts _readCompilationFacts(Compilation compilation)
    {
        var canEmitBinder = compilation.GetTypeByMetadataName(_payloadReader) is not null
            && compilation.GetTypeByMetadataName(_commandProcessor) is not null
            && compilation.GetTypeByMetadataName(_requestProcessor) is not null
            && compilation.GetTypeByMetadataName(_failFastException) is not null
            && compilation.GetTypeByMetadataName(_failFastReason) is not null
            && compilation.GetTypeByMetadataName(_commandInterface) is not null;
        return new CompilationFacts(
            compilation.GetTypeByMetadataName(_networkOptions) is not null,
            compilation.GetTypeByMetadataName(_contractRegistry) is not null,
            canEmitBinder,
            canEmitBinder
                && compilation.GetTypeByMetadataName(_streamPayloadReader) is not null
                && compilation.GetTypeByMetadataName(_codec) is not null,
            canEmitBinder
                && compilation.GetTypeByMetadataName(_failedMessage) is not null
                && compilation.GetTypeByMetadataName(_exceptionInfo) is not null,
            compilation.GetTypeByMetadataName(_payloadSender) is not null
                && compilation.GetTypeByMetadataName(_dataBus) is not null
                && compilation.GetTypeByMetadataName(_networkOptions) is not null,
            compilation.GetTypeByMetadataName(_participantDescriptor) is not null
                && compilation.GetTypeByMetadataName(_contractRegistry) is not null);
    }

    private static RetryPolicy? _readRetry(INamedTypeSymbol retryType)
    {
        var maximumCount = _readIntProperty(retryType, "MaximumDeliveryCount");
        if (maximumCount is null)
            return null;
        var secondLevel = _readBoolProperty(retryType, "SecondLevelRetriesEnabled") ?? false;
        return new RetryPolicy(maximumCount.Value, secondLevel);
    }

    private static int? _readIntProperty(INamedTypeSymbol type, string name)
    {
        var field = type.GetMembers(name).OfType<IFieldSymbol>()
            .FirstOrDefault(static candidate => candidate.IsConst && candidate.ConstantValue is int);
        if (field?.ConstantValue is int fieldValue)
            return fieldValue;

        var property = type.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
        if (property is null)
            return null;
#pragma warning disable MA0045
        foreach (var syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax declaration)
                continue;
            var expression = declaration.ExpressionBody?.Expression
                ?? declaration.Initializer?.Value
                ?? declaration.AccessorList?.Accessors
                    .SelectMany(static accessor => accessor.Body?.Statements ?? Enumerable.Empty<Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax>())
                    .OfType<ReturnStatementSyntax>()
                    .Select(static statement => statement.Expression)
                    .FirstOrDefault();
            if (expression is LiteralExpressionSyntax literal
                && int.TryParse(literal.Token.ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return value;
        }
        return null;
    }

    private static bool? _readBoolProperty(INamedTypeSymbol type, string name)
    {
        var field = type.GetMembers(name).OfType<IFieldSymbol>()
            .FirstOrDefault(static candidate => candidate.IsConst && candidate.ConstantValue is bool);
        if (field?.ConstantValue is bool fieldValue)
            return fieldValue;

        var property = type.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
        if (property is null)
            return null;
        foreach (var syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax declaration)
                continue;
            var expression = declaration.ExpressionBody?.Expression
                ?? declaration.Initializer?.Value
                ?? declaration.AccessorList?.Accessors
                    .SelectMany(static accessor => accessor.Body?.Statements ?? Enumerable.Empty<Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax>())
                    .OfType<ReturnStatementSyntax>()
                    .Select(static statement => statement.Expression)
                    .FirstOrDefault();
            if (expression is LiteralExpressionSyntax literal
                && bool.TryParse(literal.Token.ValueText, out var value))
                return value;
        }
        return null;
    }
#pragma warning restore MA0045

    private static (AttributeData? Message, AttributeData? Event, string? Name, ImmutableArray<string> FormerNames)
        _contractAttributes(INamedTypeSymbol symbol)
    {
        var message = symbol.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == _messageAttribute);
        var @event = symbol.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == _eventAttribute);
        var source = message ?? @event;
        return (message, @event, _string(source, "Name"), _strings(source, "FormerNames"));
    }

    // Equivalent to ^V\d+$ on the type name.
    private static bool _isBareVersion(string name)
    {
        return name.Length > 1 && name[0] == 'V' && name.Skip(1).All(static character => character is >= '0' and <= '9');
    }

    /// <summary>Finds <c>ICommand&lt;TSelf&gt;</c> or <c>IRequest&lt;TSelf, TResponse&gt;</c> closed over the symbol itself.</summary>
    private static INamedTypeSymbol? _selfInterface(INamedTypeSymbol symbol, string interfaceMetadataName)
    {
        return symbol.AllInterfaces.FirstOrDefault(@interface =>
            @interface.OriginalDefinition.ContainingNamespace.ToDisplayString() == _requestNamespace
            && @interface.OriginalDefinition.MetadataName == interfaceMetadataName
            && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], symbol));
    }

    private static string _contractName(INamedTypeSymbol symbol)
    {
        var attributes = _contractAttributes(symbol);
        return attributes.Name ?? _defaultContractName(symbol);
    }

    private static string _defaultContractName(INamedTypeSymbol symbol)
    {
        var group = symbol.GetAttributes()
            .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == _apiGroupAttribute)
            ?.ConstructorArguments.FirstOrDefault().Value as string ?? "Ark";
        return _normalizeLogical(group) + "." + _normalizeLogical(symbol.Name);
    }

    private static string _normalizeIdentity(string value)
    {
        return string.Join("-", _words(value).Select(static word => word.ToLowerInvariant()));
    }

    private static string _normalizeSnake(string value)
    {
        return string.Join("_", value.Split('.').SelectMany(_words).Select(static word => word.ToLowerInvariant()));
    }

    private static string _normalizeLogical(string value)
    {
        return string.Join(".", value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment => string.Join("-", _words(segment).Select(static word => word.ToLowerInvariant()))));
    }

    private static IEnumerable<string> _words(string value)
    {
        var word = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var startsWord = index > 0
                && char.IsUpper(character)
                && (char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1])));
            if (startsWord && word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
            if (char.IsLetterOrDigit(character))
                word.Append(character);
            else if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }
        if (word.Length > 0)
            yield return word.ToString();
    }

    private static bool _isLogicalName(string value)
    {
        if (value.Length == 0 || value[0] is '-' or '_' or '.' or '/' || value[^1] is '-' or '_' or '.' or '/')
            return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.' or '/')
                || ((character is '-' or '_' or '.' or '/') && index > 0
                    && (value[index - 1] is '-' or '_' or '.' or '/')))
                return false;
        }
        return true;
    }

    private static bool _isNormalized(string value)
    {
        return _isLogicalName(value);
    }

    private static void _emitNetwork(
        GenerationSink context,
        Network network,
        CompilationFacts facts)
    {
        if (!_validateDeclaringType(context, network.Symbol, "MessagingNetwork"))
            return;

        var participants = network.Members
            .Select(static member => member.Participant)
            .Where(static participant => participant is not null)
            .Select(static participant => participant!.Value)
            .ToArray();
        var processors = participants
            .SelectMany(static participant => participant.Processes.Select(contract => (contract, participant)))
            .GroupBy(static item => item.contract)
            .ToDictionary(static group => group.Key, static group => group.First().participant);
        var publishers = participants
            .SelectMany(static participant => participant.Publishes.Select(contract => (contract, participant)))
            .GroupBy(static item => item.contract)
            .ToDictionary(static group => group.Key, static group => group.First().participant);
        var contracts = processors.Keys
            .Concat(publishers.Keys)
            .Distinct()
            .OrderBy(static contract => contract.DisplayName, StringComparer.Ordinal)
            .ToArray();
        var name = network.Symbol.Name;
        var source = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("using global::System;")
            .AppendLine("using global::System.Collections.Generic;")
            .AppendLine("using global::System.Collections.Frozen;");
        if (network.Symbol.Namespace is not null)
        {
            source.Append("namespace ").Append(network.Symbol.Namespace).AppendLine(";")
                .AppendLine();
        }

        source.AppendLine("[global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append(network.Symbol.Accessibility).Append(" sealed partial class ").Append(name)
            .Append(" : global::Ark.Tools.MediatorFramework.Messaging.IMessagingNetwork<").Append(name).AppendLine(">")
            .AppendLine("{");
        source
            .AppendLine("    /// <summary>Gets the resolved identity of this messaging network.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public static string NetworkIdentity => \"").Append(_escape(network.Name)).AppendLine("\";")
            .AppendLine();

        if (facts.HasNetworkOptions)
        {
            source
                .AppendLine("    /// <summary>Creates the resolved options for this messaging network.</summary>")
                .AppendLine("    /// <returns>The resolved messaging network options.</returns>")
                .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                .AppendLine("    public static global::Ark.Tools.MediatorFramework.Messaging.MessagingNetworkOptions CreateOptions()")
                .AppendLine("    {")
                .AppendLine("        return new global::Ark.Tools.MediatorFramework.Messaging.MessagingNetworkOptions(")
                .Append("            typeof(").Append(name).AppendLine("),")
                .AppendLine("            new global::Ark.Tools.MediatorFramework.MessagingNetworkAttribute")
                .AppendLine("            {")
                .AppendLine("                Members = new global::System.Type[]");
            source.AppendLine("                {");
            foreach (var member in network.Members)
                source.Append("                    typeof(").Append(member.Symbol.TypeName).AppendLine("),");
            source.AppendLine("                },")
                .Append("                Requires = (global::Ark.Tools.MediatorFramework.MessagingCapabilities)")
                .Append(network.Requires.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
                .Append("                MaximumDecompressedPayloadBytes = ")
                .Append(network.MaximumDecompressedPayloadBytes?.ToString(CultureInfo.InvariantCulture)
                    ?? "global::Ark.Tools.MediatorFramework.MessagingNetworkAttribute.DefaultMaximumDecompressedPayloadBytes").AppendLine(",")
                .Append("                DataBusMaximumAttachmentBytes = ")
                .Append(network.DataBusMaximumAttachmentBytes?.ToString(CultureInfo.InvariantCulture)
                    ?? "global::Ark.Tools.MediatorFramework.MessagingNetworkAttribute.DefaultDataBusMaximumAttachmentBytes").AppendLine(",")
                .Append("                MaximumSchedulingDelay = global::System.TimeSpan.FromSeconds(")
                .Append(network.MaximumSchedulingDelaySeconds?.ToString(CultureInfo.InvariantCulture)
                    ?? "global::Ark.Tools.MediatorFramework.MessagingNetworkAttribute.DefaultMaximumSchedulingDelaySeconds").AppendLine("),")
                .Append("                ResourceLifecycle = (global::Ark.Tools.MediatorFramework.MessagingResourceLifecycle)")
                .Append(network.ResourceLifecycle?.ToString(CultureInfo.InvariantCulture)
                    ?? "global::Ark.Tools.MediatorFramework.MessagingResourceLifecycle.CreateIfMissing").AppendLine(",");
            source
                .AppendLine("            });")
                .AppendLine("    }")
                .AppendLine();
        }

        source
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, string> _destinations =")
            .AppendLine("        new Dictionary<Type, string>")
            .AppendLine("        {");

        foreach (var contract in contracts)
        {
            var destination = processors.TryGetValue(contract, out var processor)
                ? processor.Identity
                : publishers[contract].Identity + "-" + contract.ContractName;
            source.Append("            [typeof(").Append(contract.TypeName).Append(")] = \"")
                .Append(_escape(destination)).AppendLine("\",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, string> _processors =")
            .AppendLine("        new Dictionary<Type, string>")
            .AppendLine("        {");
        foreach (var contract in processors.Keys
            .OrderBy(static contract => contract.DisplayName, StringComparer.Ordinal))
        {
            source.Append("            [typeof(").Append(contract.TypeName).Append(")] = \"")
               .Append(_escape(processors[contract].Identity)).AppendLine("\",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, string> _publishers =")
            .AppendLine("        new Dictionary<Type, string>")
            .AppendLine("        {");
        foreach (var contract in publishers.Keys
            .OrderBy(static contract => contract.DisplayName, StringComparer.Ordinal))
        {
            source.Append("            [typeof(").Append(contract.TypeName).Append(")] = \"")
               .Append(_escape(publishers[contract].Identity)).AppendLine("\",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, global::Ark.Tools.MediatorFramework.SerializationProtocol> _wireProtocols =")
            .AppendLine("        new Dictionary<Type, global::Ark.Tools.MediatorFramework.SerializationProtocol>")
            .AppendLine("        {");
        foreach (var contract in contracts)
        {
            var owner = processors.TryGetValue(contract, out var processor)
                ? processor
                : publishers[contract];
            source.Append("            [typeof(").Append(contract.TypeName).Append(")] = global::Ark.Tools.MediatorFramework.SerializationProtocol.")
                .Append(_protocolName(owner.DefaultSerializer)).AppendLine(",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, string> _logicalNames =")
            .AppendLine("        new Dictionary<Type, string>")
            .AppendLine("        {");
        foreach (var contract in contracts)
        {
            source.Append("            [typeof(").Append(contract.TypeName).Append(")] = \"")
                .Append(_escape(contract.ContractName)).AppendLine("\",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
        .AppendLine("    /// <summary>Gets the destination for a declared contract.</summary>")
        .AppendLine("    /// <typeparam name=\"T\">The declared contract type.</typeparam>")
        .AppendLine("    /// <returns>The participant queue or event topic.</returns>")
        .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    public static string GetDestinationFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        return GetDestination(typeof(T));")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the processing participant for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The message contract type.</typeparam>")
            .AppendLine("    /// <returns>The processing participant identity.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    public static string GetProcessorIdentityFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        return GetProcessorIdentity(typeof(T));")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the publishing participant for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The event contract type.</typeparam>")
            .AppendLine("    /// <returns>The publishing participant identity.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    public static string GetPublisherIdentityFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        return GetPublisherIdentity(typeof(T));")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the destination for a runtime contract type.</summary>")
            .AppendLine("    /// <param name=\"contractType\">The declared contract type.</param>")
            .AppendLine("    /// <returns>The participant queue or event topic.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static string GetDestination(global::System.Type contractType)")
            .AppendLine("    {")
            .AppendLine("        if (_destinations.TryGetValue(contractType, out var destination))")
            .AppendLine("            return destination;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(contractType, NetworkIdentity);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the processing participant for a runtime contract type.</summary>")
            .AppendLine("    /// <param name=\"contractType\">The message contract type.</param>")
            .AppendLine("    /// <returns>The processing participant identity.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static string GetProcessorIdentity(global::System.Type contractType)")
            .AppendLine("    {")
            .AppendLine("        if (_processors.TryGetValue(contractType, out var processor))")
            .AppendLine("            return processor;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(contractType, NetworkIdentity);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the publishing participant for a runtime contract type.</summary>")
            .AppendLine("    /// <param name=\"contractType\">The event contract type.</param>")
            .AppendLine("    /// <returns>The publishing participant identity.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static string GetPublisherIdentity(global::System.Type contractType)")
            .AppendLine("    {")
            .AppendLine("        if (_publishers.TryGetValue(contractType, out var publisher))")
            .AppendLine("            return publisher;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(contractType, NetworkIdentity);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the owner-selected wire protocol for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The declared contract type.</typeparam>")
            .AppendLine("    /// <returns>The owner-selected serialization protocol.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    public static global::Ark.Tools.MediatorFramework.SerializationProtocol GetWireProtocolFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        return GetWireProtocol(typeof(T));")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the owner-selected protocol for a runtime contract type.</summary>")
            .AppendLine("    /// <param name=\"contractType\">The declared contract type.</param>")
            .AppendLine("    /// <returns>The owner-selected serialization protocol.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static global::Ark.Tools.MediatorFramework.SerializationProtocol GetWireProtocol(global::System.Type contractType)")
            .AppendLine("    {")
            .AppendLine("        if (_wireProtocols.TryGetValue(contractType, out var protocol))")
            .AppendLine("            return protocol;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(contractType, NetworkIdentity);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the current logical wire name for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The declared contract type.</typeparam>")
            .AppendLine("    /// <returns>The normalized logical contract name.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    public static string GetLogicalNameFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        return GetLogicalName(typeof(T));")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <summary>Gets the current logical name for a runtime contract type.</summary>")
            .AppendLine("    /// <param name=\"contractType\">The declared contract type.</param>")
            .AppendLine("    /// <returns>The normalized logical contract name.</returns>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static string GetLogicalName(global::System.Type contractType)")
            .AppendLine("    {")
            .AppendLine("        if (_logicalNames.TryGetValue(contractType, out var logicalName))")
            .AppendLine("            return logicalName;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(contractType, NetworkIdentity);")
            .AppendLine("    }");

        if (facts.HasContractRegistry)
        {
            source.AppendLine()
                .AppendLine("    private sealed class GeneratedRegistry : global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry")
                .AppendLine("    {")
                .Append("        string global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry.NetworkIdentity => ").Append(name).AppendLine(".NetworkIdentity;")
                .Append("        string global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry.GetDestination<T>() where T : class => ").Append(name).AppendLine(".GetDestinationFor<T>();")
                .Append("        string global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry.GetProcessorIdentity<T>() where T : class => ").Append(name).AppendLine(".GetProcessorIdentityFor<T>();")
                .Append("        string global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry.GetPublisherIdentity<T>() where T : class => ").Append(name).AppendLine(".GetPublisherIdentityFor<T>();")
                .Append("        global::Ark.Tools.MediatorFramework.SerializationProtocol global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry.GetWireProtocol<T>() where T : class => ").Append(name).AppendLine(".GetWireProtocolFor<T>();")
                .Append("        string global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry.GetLogicalName<T>() where T : class => ").Append(name).AppendLine(".GetLogicalNameFor<T>();")
                .AppendLine("    }")
                .AppendLine()
                .AppendLine("    /// <summary>Gets the generated transport-neutral contract registry.</summary>")
                .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                .AppendLine("    public static global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry Registry { get; } = new GeneratedRegistry();");
        }

        source
            .AppendLine("}");

        context.AddSource(
            _safeIdentifier(network.Symbol.DisplayName) + "_" + _stableHash(network.Symbol.DisplayName) + ".Registry.g.cs",
            source.ToString());
    }

    private static void _emitParticipant(
        GenerationSink context,
        Participant participant,
        IReadOnlyList<Participant> networkMembers,
        CompilationFacts facts)
    {
        if (!_validateDeclaringType(context, participant.Symbol, "MessagingParticipant"))
            return;

        var source = new StringBuilder()
            .AppendLine("// <auto-generated />");
        if (participant.Symbol.Namespace is not null)
        {
            source.Append("namespace ").Append(participant.Symbol.Namespace).AppendLine(";")
                .AppendLine();
        }

        var canEmitBinder = facts.CanEmitBinder;
        var canEmitStreamBinder = facts.CanEmitStreamBinder;
        var canEmitFailedBinder = facts.CanEmitFailedBinder;
        var canEmitPayloadSender = facts.CanEmitPayloadSender;
        var canEmitDescriptor = facts.CanEmitDescriptor;

        source.AppendLine("[global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append(participant.Symbol.Accessibility).Append(" partial class ").Append(participant.Symbol.Name)
            .Append(" : global::Ark.Tools.MediatorFramework.Messaging.IMessagingParticipant<")
            .Append(participant.Symbol.TypeName).AppendLine(">")
            .AppendLine("{")
            .AppendLine("    /// <summary>Gets the resolved identity of this messaging participant.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public const string Identity = \"").Append(_escape(participant.Identity)).AppendLine("\";")
            .AppendLine("    /// <summary>Gets the sender-side compression algorithm.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public const global::Ark.Tools.MediatorFramework.CompressionAlgorithm Compression = global::Ark.Tools.MediatorFramework.CompressionAlgorithm.")
            .Append(participant.CompressionName).AppendLine(";")
            .AppendLine("    /// <summary>Gets the minimum payload size eligible for compression.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public const int CompressionMinimumSizeBytes = ")
            .Append(participant.CompressionMinimumSizeBytes.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        if (canEmitPayloadSender)
        {
            source.AppendLine()
                .AppendLine("    /// <summary>Creates a payload sender using this participant's compression settings.</summary>")
                .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                .AppendLine("    public static global::Ark.Tools.MediatorFramework.Messaging.MessagingPayloadSender CreatePayloadSender(")
                .AppendLine("        global::Ark.Tools.MediatorFramework.IMessagingDataBus dataBus,")
                .AppendLine("        global::Ark.Tools.MediatorFramework.Messaging.MessagingNetworkOptions network)")
                .AppendLine("    {")
                .AppendLine("        return new global::Ark.Tools.MediatorFramework.Messaging.MessagingPayloadSender(")
                .AppendLine("            dataBus, network, Compression, CompressionMinimumSizeBytes);")
                .AppendLine("    }");
        }

        var contracts = participant.Processes
            .Concat(participant.Subscribes)
            .Distinct()
            .Where(static contract => contract.IsSelfCommand || contract.RequestResponseTypeName is not null)
            .OrderBy(static contract => contract.DisplayName, StringComparer.Ordinal)
            .ToArray();

        if (canEmitBinder && contracts.Length > 0)
        {
            source.AppendLine()
                .AppendLine("    /// <summary>Dispatches a received contract by its current or former logical name.</summary>")
                .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                .AppendLine("    public static async global::System.Threading.Tasks.Task DispatchAsync(")
                .AppendLine("        string logicalName,")
                .AppendLine("        global::Ark.Tools.MediatorFramework.Messaging.IMessagingPayloadReader payload,")
                .AppendLine("        global::Ark.Tools.Solid.ICommandProcessor processor,")
                .AppendLine("        global::Ark.Tools.Solid.IRequestProcessor requestProcessor,")
                .AppendLine("        global::System.Threading.CancellationToken ctk)")
                .AppendLine("    {")
                .AppendLine("        switch (logicalName)")
                .AppendLine("        {");
            foreach (var contract in contracts)
            {
                var names = new[] { contract.ContractName }
                    .Concat(contract.FormerNames)
                    .Distinct(StringComparer.Ordinal);
                foreach (var wireName in names)
                    source.Append("            case \"").Append(_escape(wireName)).AppendLine("\":");
                source.AppendLine("            {")
                    .Append("                var message = await payload.DeserializeAsync<").Append(contract.TypeName).AppendLine(">(ctk).ConfigureAwait(false);");
                if (contract.RequestResponseTypeName is null)
                    source.Append("                await processor.ExecuteAsync<").Append(contract.TypeName).AppendLine(">(message, ctk).ConfigureAwait(false);");
                else
                    source.Append("                await requestProcessor.ExecuteAsync<").Append(contract.TypeName).Append(", ")
                        .Append(contract.RequestResponseTypeName).AppendLine(">(message, ctk).ConfigureAwait(false);");
                source.AppendLine("                break;")
                    .AppendLine("            }");
            }

            source.AppendLine("            default:")
                .AppendLine("                throw new global::Ark.Tools.MediatorFramework.Messaging.MessagingFailFastException(")
                .AppendLine("                    global::Ark.Tools.MediatorFramework.Messaging.MessagingFailFastReason.UnknownContractName,")
                .AppendLine("                    logicalName);")
                .AppendLine("        }")
                .AppendLine("    }");

            if (canEmitStreamBinder)
            {
                source.AppendLine()
                    .AppendLine("    /// <summary>Dispatches a prepared stream through the generated contract binder.</summary>")
                    .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                    .AppendLine("    public static async global::System.Threading.Tasks.Task DispatchAsync(")
                    .AppendLine("        string logicalName,")
                    .AppendLine("        global::System.IO.Stream payload,")
                    .AppendLine("        global::Ark.Tools.MediatorFramework.Messaging.IMessagingCodec codec,")
                    .AppendLine("        global::Ark.Tools.Solid.ICommandProcessor processor,")
                    .AppendLine("        global::Ark.Tools.Solid.IRequestProcessor requestProcessor,")
                    .AppendLine("        global::System.Threading.CancellationToken ctk)")
                    .AppendLine("    {")
                    .AppendLine("        var reader = new global::Ark.Tools.MediatorFramework.Messaging.MessagingStreamPayloadReader(payload, codec);")
                    .AppendLine("        await using (global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false))")
                    .AppendLine("        {")
                    .AppendLine("            await DispatchAsync(logicalName, reader, processor, requestProcessor, ctk).ConfigureAwait(false);")
                    .AppendLine("        }")
                    .AppendLine("    }");
            }

            if (canEmitFailedBinder)
            {
                source.AppendLine()
                    .AppendLine("    /// <summary>Dispatches an inline second-level failure by logical name.</summary>")
                    .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                    .AppendLine("    public static async global::System.Threading.Tasks.Task DispatchFailedAsync(")
                    .AppendLine("        string logicalName,")
                    .AppendLine("        global::Ark.Tools.MediatorFramework.Messaging.IMessagingPayloadReader payload,")
                    .AppendLine("        int deliveryCount,")
                    .AppendLine("        global::Ark.Tools.MediatorFramework.MessagingExceptionInfo error,")
                    .AppendLine("        global::Ark.Tools.Solid.ICommandProcessor processor,")
                    .AppendLine("        global::System.Threading.CancellationToken ctk)")
                    .AppendLine("    {")
                    .AppendLine("        switch (logicalName)")
                    .AppendLine("        {");
                foreach (var contract in contracts)
                {
                    var names = new[] { contract.ContractName }
                        .Concat(contract.FormerNames)
                        .Distinct(StringComparer.Ordinal);
                    foreach (var wireName in names)
                        source.Append("            case \"").Append(_escape(wireName)).AppendLine("\":");
                    source.AppendLine("            {")
                        .Append("                var message = await payload.DeserializeAsync<").Append(contract.TypeName).AppendLine(">(ctk).ConfigureAwait(false);")
                        .Append("                var failed = new global::Ark.Tools.MediatorFramework.MessagingFailed<")
                        .Append(contract.TypeName).AppendLine(">(message, deliveryCount, new[] { error });")
                        .Append("                await processor.ExecuteAsync<global::Ark.Tools.MediatorFramework.MessagingFailed<")
                        .Append(contract.TypeName).AppendLine(">>(failed, ctk).ConfigureAwait(false);")
                        .AppendLine("                return;")
                        .AppendLine("            }");
                }

                source.AppendLine("            default:")
                    .AppendLine("                throw new global::Ark.Tools.MediatorFramework.Messaging.MessagingFailFastException(")
                    .AppendLine("                    global::Ark.Tools.MediatorFramework.Messaging.MessagingFailFastReason.UnknownContractName,")
                    .AppendLine("                    logicalName);")
                    .AppendLine("        }")
                    .AppendLine("    }");
            }
        }

        if (canEmitDescriptor)
        {
            source.AppendLine()
                .AppendLine("    /// <summary>Creates the generated runtime descriptor for this participant.</summary>")
                .AppendLine("    /// <param name=\"network\">The resolved network options.</param>")
                .AppendLine("    /// <param name=\"registry\">The generated network registry.</param>")
                .AppendLine("    /// <returns>The participant runtime descriptor.</returns>")
                .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                .AppendLine("    public static global::Ark.Tools.MediatorFramework.Messaging.MessagingParticipantDescriptor CreateDescriptor(")
                .AppendLine("        global::Ark.Tools.MediatorFramework.Messaging.MessagingNetworkOptions network,")
                .AppendLine("        global::Ark.Tools.MediatorFramework.Messaging.IMessagingContractRegistry registry)")
                .AppendLine("    {")
                .AppendLine("        return new global::Ark.Tools.MediatorFramework.Messaging.MessagingParticipantDescriptor(")
                .Append("            typeof(").Append(participant.Symbol.TypeName).AppendLine("),")
                .AppendLine("            network,")
                .AppendLine("            registry,")
                .AppendLine("            Identity,")
                .AppendLine("            new global::Ark.Tools.MediatorFramework.SerializationProtocol[]")
                .AppendLine("            {");
            foreach (var serializer in participant.Serializers)
            {
                source.Append("                (global::Ark.Tools.MediatorFramework.SerializationProtocol)")
                    .Append(serializer.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            }
            source.AppendLine("            },")
                .Append("            ")
                .Append(participant.RetryTypeName is null
                    ? "global::Ark.Tools.MediatorFramework.Messaging.MessagingDefaultRetryPolicy.Instance"
                    : "new " + participant.RetryTypeName + "()")
                .AppendLine(",")
                .AppendLine("            Compression,")
                .AppendLine("            CompressionMinimumSizeBytes,")
                .Append("            ").Append(contracts.Length > 0 ? "true" : "false").AppendLine(",");
            if (contracts.Length > 0)
            {
                source.AppendLine("            DispatchAsync,")
                    .Append("            ")
                    .Append(participant.Retry?.SecondLevelRetriesEnabled == true
                        ? "DispatchFailedAsync"
                        : "null")
                    .AppendLine(",")
                    .AppendLine("            new global::System.Type[]")
                    .AppendLine("            {");
                foreach (var contract in contracts)
                {
                    if (contract.RequestResponseTypeName is null)
                        source.Append("                typeof(global::Ark.Tools.Solid.ICommandHandler<")
                            .Append(contract.TypeName).AppendLine(">),");
                    else
                        source.Append("                typeof(global::Ark.Tools.Solid.IRequestHandler<")
                            .Append(contract.TypeName).Append(", ")
                            .Append(contract.RequestResponseTypeName).AppendLine(">),");
                    if (participant.Retry?.SecondLevelRetriesEnabled == true)
                    {
                        source.Append("                typeof(global::Ark.Tools.Solid.ICommandHandler<global::Ark.Tools.MediatorFramework.MessagingFailed<")
                            .Append(contract.TypeName).AppendLine(">>),");
                    }
                }
                source.AppendLine("            },");
            }
            else
            {
                source.AppendLine("            null,")
                    .AppendLine("            null,")
                    .AppendLine("            global::System.Array.Empty<global::System.Type>(),");
            }
            source.AppendLine("            new global::Ark.Tools.MediatorFramework.Messaging.MessagingTopicResource[]")
                .AppendLine("            {");
            foreach (var contract in participant.Publishes)
            {
                source.Append("                new global::Ark.Tools.MediatorFramework.Messaging.MessagingTopicResource(\"")
                    .Append(_escape(participant.Identity + "-" + contract.ContractName))
                    .Append("\", \"").Append(_escape(participant.Identity)).AppendLine("\"),");
            }
            // Subscribed topics are named and owned by the single publisher; other cases already report a diagnostic.
            source.AppendLine("            },")
                .AppendLine("            new global::Ark.Tools.MediatorFramework.Messaging.MessagingTopicResource[]")
                .AppendLine("            {");
            foreach (var contract in participant.Subscribes)
            {
                var publishers = networkMembers
                    .Where(member => member.Publishes.Contains(contract))
                    .ToArray();
                if (publishers.Length != 1)
                    continue;
                source.Append("                new global::Ark.Tools.MediatorFramework.Messaging.MessagingTopicResource(\"")
                    .Append(_escape(publishers[0].Identity + "-" + contract.ContractName))
                    .Append("\", \"").Append(_escape(publishers[0].Identity)).AppendLine("\"),");
            }
            source.AppendLine("            },")
                .AppendLine("            new string[]")
                .AppendLine("            {");
            foreach (var topic in networkMembers
                .SelectMany(static member => member.Publishes.Select(contract => member.Identity + "-" + contract.ContractName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static topic => topic, StringComparer.Ordinal))
            {
                source.Append("                \"").Append(_escape(topic)).AppendLine("\",");
            }
            source.AppendLine("            });");
            source.AppendLine("    }");
        }

        source.AppendLine("}");
        context.AddSource(
            _safeIdentifier(participant.Symbol.DisplayName) + "_" + _stableHash(participant.Symbol.DisplayName) + ".Participant.g.cs",
            source.ToString());
    }

    private static bool _validateDeclaringType(
        GenerationSink context,
        TypeSpec symbol,
        string attributeName)
    {
        if (!symbol.IsValidDeclaringType)
        {
            _report(context, _nonPartialDeclaringType, symbol, symbol.DisplayName, attributeName);
            return false;
        }
        return true;
    }

    private static bool _isValidDeclaringType(INamedTypeSymbol symbol)
    {
#pragma warning disable MA0045
        var isPartial = symbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
#pragma warning restore MA0045
        return symbol.ContainingType is null
            && symbol.Arity == 0
            && isPartial;
    }

    private static string _typeName(INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string _accessibility(INamedTypeSymbol symbol)
    {
        return symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };
    }

    private static string _protocolName(int protocol)
    {
        return protocol switch
        {
            0 => "Json",
            1 => "MessagePack",
            2 => "Protobuf",
            _ => "Json",
        };
    }

    private static string _compressionName(Compilation compilation, int compression)
    {
        var enumType = compilation.GetTypeByMetadataName("Ark.Tools.MediatorFramework.CompressionAlgorithm");
        var member = enumType?.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && field.ConstantValue is int value && value == compression);
        return member?.Name ?? "None";
    }

    private static void _emitMetadata(GenerationSink context, IReadOnlyList<Network> networks)
    {
        var source = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("namespace Ark.Tools.MediatorFramework.Generated;");
        foreach (var network in networks)
        {
            var displayName = network.Symbol.DisplayName;
            var name = _safeIdentifier(displayName) + "_" + _stableHash(displayName) + "MessagingDescriptor";
            source.Append("internal static class ").Append(name).AppendLine()
                .AppendLine("{")
                .Append("    internal const string Network = \"").Append(_escape(network.Name)).AppendLine("\";")
                .Append("    internal static readonly string[] Members = new string[] { ")
                .Append(string.Join(", ", network.Members.Select(static member => "\"" + _escape(member.Symbol.DisplayName) + "\"")))
                .AppendLine(" };")
                .AppendLine("}");
        }
        context.AddSource("ArkMessagingMetadata.g.cs", source.ToString());
    }

    private static string _safeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        return builder.ToString();
    }

    private static string _escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string _stableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
                hash = (hash ^ character) * 16777619u;
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    private static void _reportDuplicates(
        GenerationSink context,
        Participant participant,
        EquatableArray<ContractSpec> contracts,
        string listName)
    {
        foreach (var duplicate in contracts.GroupBy(static contract => contract).Where(static group => group.Count() > 1))
            _report(context, _duplicateParticipantContract, participant.Symbol, participant.Identity, duplicate.Key.DisplayName, listName);
    }

    private static void _add(
        IDictionary<ContractSpec, List<Participant>> map,
        ContractSpec contract,
        Participant participant)
    {
        if (!map.TryGetValue(contract, out var values))
            map.Add(contract, values = new List<Participant>());
        values.Add(participant);
    }

    private static void _report(
        GenerationSink context,
        DiagnosticDescriptor descriptor,
        TypeSpec symbol,
        params object[] arguments)
    {
        context._report(descriptor, symbol.Location, arguments);
    }

    private static void _report(
        GenerationSink context,
        DiagnosticDescriptor descriptor,
        ContractSpec contract,
        params object[] arguments)
    {
        context._report(descriptor, contract.Location, arguments);
    }

    private static ImmutableArray<INamedTypeSymbol> _types(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        if (value.Kind != TypedConstantKind.Array)
            return ImmutableArray<INamedTypeSymbol>.Empty;
        return value.Values
            .Where(static item => item.Value is INamedTypeSymbol)
            .Select(static item => (INamedTypeSymbol)item.Value!)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> _strings(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return ImmutableArray<string>.Empty;
        var value = _named(attribute, name);
        if (value.Kind != TypedConstantKind.Array)
            return ImmutableArray<string>.Empty;
        return value.Values.Where(static item => item.Value is string).Select(static item => (string)item.Value!).ToImmutableArray();
    }

    private static ImmutableArray<int> _enums(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        if (value.Kind != TypedConstantKind.Array)
            return ImmutableArray<int>.Empty;
        return value.Values.Select(static item => (int)item.Value!).ToImmutableArray();
    }

    private static int _enum(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        return value.Value is null ? default : (int)value.Value;
    }

    private static int _int(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        return value.Value is int integer ? integer : default;
    }

    private static int? _optionalInt(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        return value.Value is int integer ? integer : null;
    }

    private static string? _string(AttributeData? attribute, string name)
    {
        var value = attribute is null ? default : _named(attribute, name);
        return value.Value as string;
    }

    private static INamedTypeSymbol? _type(AttributeData attribute, string name)
    {
        return _named(attribute, name).Value as INamedTypeSymbol;
    }

    private static TypedConstant _named(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
                return argument.Value;
        }
        return default;
    }

    private static DiagnosticDescriptor _descriptor(string id)
    {
        return id switch
        {
            "ARKMSG001" => _duplicateMember,
            "ARKMSG002" => _missingParticipant,
            "ARKMSG003" => _dualContract,
            "ARKMSG004" => _multipleNetworks,
            "ARKMSG005" => _multipleProcessor,
            "ARKMSG006" => _multiplePublisher,
            "ARKMSG007" => _unwiredContract,
            "ARKMSG008" => _unsatisfiableSubscription,
            "ARKMSG009" => _serializerMismatch,
            "ARKMSG010" => _defaultSerializer,
            "ARKMSG011" => _missingCapability,
            "ARKMSG012" => _crossNetworkContract,
            "ARKMSG013" => _invalidIdentity,
            "ARKMSG014" => _duplicateIdentity,
            "ARKMSG015" => _reservedIdentity,
            "ARKMSG017" => _invalidRetry,
            "ARKMSG018" => _invalidEventShape,
            "ARKMSG027" => _undispatchableMessage,
            "ARKMSG028" => _duplicateParticipantContract,
            "ARKMSG019" => _nonNormalizedName,
            "ARKMSG020" => _duplicateName,
            "ARKMSG021" => _duplicateAlias,
            "ARKMSG022" => _aliasCollision,
            "ARKMSG023" => _nonPartialDeclaringType,
            "ARKMSG025" => MessagingContractTopologyValidator._missingMessagePackShape,
            "ARKMSG026" => MessagingContractTopologyValidator._missingProtobufShape,
            "ARKMSG029" => _versionOnlyDefaultName,
            _ => throw new InvalidOperationException("Unknown messaging diagnostic: " + id),
        };
    }

    private sealed class GenerationSink
    {
        private readonly List<GeneratedSourceSpec> _sources = new();
        private readonly List<DiagnosticSpec> _diagnostics = new();

        public GenerationSink(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public void AddSource(string hintName, string source)
        {
            _sources.Add(new GeneratedSourceSpec(
                hintName,
                source.Replace("\r\n", "\n").Replace('\r', '\n')));
        }

        public void _report(DiagnosticDescriptor descriptor, LocationSpec? location, params object[] arguments)
        {
            _diagnostics.Add(new DiagnosticSpec(
                descriptor.Id,
                location,
                arguments.Select(static argument =>
                        Convert.ToString(argument, CultureInfo.InvariantCulture) ?? string.Empty)
                    .ToImmutableArray()));
        }

        public MessagingNetworkAggregateSpec _toSpec()
        {
            return new MessagingNetworkAggregateSpec(
                _sources.OrderBy(static source => source.HintName, StringComparer.Ordinal).ToImmutableArray(),
                _diagnostics.ToImmutableArray());
        }
    }

    private sealed record MessagingNetworkAggregateSpec(
        EquatableArray<GeneratedSourceSpec> Sources,
        EquatableArray<DiagnosticSpec> Diagnostics);

    private readonly record struct GeneratedSourceSpec(string HintName, string Source);

    /// <summary>The symbol facts of a network, participant or contract type.</summary>
    private sealed record TypeSpec(
        string Key,
        string DisplayName,
        string TypeName,
        string Name,
        string? Namespace,
        string Accessibility,
        bool IsValidDeclaringType,
        LocationSpec? Location);

    /// <summary>The symbol facts of a messaging contract.</summary>
    private sealed record ContractSpec(
        TypeSpec Symbol,
        string ContractName,
        string DefaultContractName,
        bool HasMessage,
        bool HasEvent,
        string? ExplicitName,
        EquatableArray<string> FormerNames,
        bool HasVersionOnlyDefaultName,
        bool IsSelfCommand,
        string? RequestResponseTypeName,
        bool HasMessagePackAttribute,
        bool HasGoogleProtobufShape)
    {
        public string DisplayName => Symbol.DisplayName;

        public string TypeName => Symbol.TypeName;

        public LocationSpec? Location => Symbol.Location;
    }

    /// <summary>The runtime types available to the compilation that gate optional generated members.</summary>
    private readonly record struct CompilationFacts(
        bool HasNetworkOptions,
        bool HasContractRegistry,
        bool CanEmitBinder,
        bool CanEmitStreamBinder,
        bool CanEmitFailedBinder,
        bool CanEmitPayloadSender,
        bool CanEmitDescriptor);

    private readonly record struct NetworkMember(TypeSpec Symbol, Participant? Participant);

    private readonly record struct Network(
        TypeSpec Symbol,
        string Name,
        EquatableArray<NetworkMember> Members,
        int Requires,
        int? MaximumDecompressedPayloadBytes,
        int? DataBusMaximumAttachmentBytes,
        int? MaximumSchedulingDelaySeconds,
        int? ResourceLifecycle);

    private readonly record struct Participant(
        TypeSpec Symbol,
        string Identity,
        EquatableArray<ContractSpec> Processes,
        EquatableArray<ContractSpec> Publishes,
        EquatableArray<ContractSpec> Subscribes,
        EquatableArray<int> Serializers,
        int DefaultSerializer,
        int Compression,
        string CompressionName,
        int CompressionMinimumSizeBytes,
        string? RetryTypeName,
        RetryPolicy? Retry,
        EquatableArray<ContractSpec> Contracts);

    private readonly record struct RetryPolicy(int MaximumDeliveryCount, bool SecondLevelRetriesEnabled);
}
