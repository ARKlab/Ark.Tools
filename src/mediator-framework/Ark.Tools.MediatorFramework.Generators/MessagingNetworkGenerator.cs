// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

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
    private const string _failFastException = "Ark.Tools.MediatorFramework.Messaging.MessagingFailFastException";
    private const string _failFastReason = "Ark.Tools.MediatorFramework.Messaging.MessagingFailFastReason";
    private const string _failedMessage = "Ark.Tools.MediatorFramework.MessagingFailed`1";
    private const string _exceptionInfo = "Ark.Tools.MediatorFramework.MessagingExceptionInfo";
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
    private static readonly DiagnosticDescriptor _longTopic = _rule(
        "ARKMSG016", "Event topic name is too long",
        "Event topic '{0}' exceeds the Service Bus 260-character entity limit", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidRetry = _rule(
        "ARKMSG017", "Invalid messaging retry policy",
        "Retry policy for participant '{0}' must have MaximumDeliveryCount >= {1}", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidEventShape = _rule(
        "ARKMSG018", "Invalid event contract",
        "Event contract '{0}' must implement ICommand<TSelf> or IRequest<TSelf, TResponse>", DiagnosticSeverity.Error);
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
    private static readonly DiagnosticDescriptor _nonStaticNetwork = _rule(
        "ARKMSG024", "Messaging network must be static",
        "Type '{0}' is marked with [MessagingNetwork] but is not declared as a static class. Add the 'static' modifier.",
        DiagnosticSeverity.Error);
    private static DiagnosticDescriptor _rule(string id, string title, string message, DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(id, title, message, "Ark.Tools.MediatorFramework", severity, true);
    }

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var networks = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                _networkAttribute,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var participants = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                _participantAttribute,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();

        context.RegisterSourceOutput(
            networks.Combine(participants).Combine(context.CompilationProvider),
            static (productionContext, input) =>
            {
                var ((networkSymbols, participantSymbols), compilation) = input;
                _emit(
                    productionContext,
                    networkSymbols.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>(),
                    participantSymbols.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>(),
                    compilation);
            });
    }

    private static void _emit(
        SourceProductionContext context,
        IEnumerable<INamedTypeSymbol> symbols,
        IEnumerable<INamedTypeSymbol> participantSymbols,
        Compilation compilation)
    {
        var networks = symbols
            .Select(_readNetwork)
            .OrderBy(network => network.Symbol.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();
        var participantNetworks = new Dictionary<INamedTypeSymbol, List<Network>>(SymbolEqualityComparer.Default);
        var allContracts = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var network in networks)
        {
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var participants = new List<Participant>();
            foreach (var member in network.MemberSymbols)
            {
                if (!seen.Add(member))
                {
                    _report(context, _duplicateMember, network.Symbol, network.Name, member.ToDisplayString());
                    continue;
                }

                var participant = _readParticipant(member);
                if (participant is null)
                {
                    _report(context, _missingParticipant, network.Symbol, network.Name, member.ToDisplayString());
                    continue;
                }

                participants.Add(participant.Value);
                if (!participantNetworks.TryGetValue(member, out var memberships))
                    participantNetworks.Add(member, memberships = new List<Network>());
                memberships.Add(network);
                foreach (var contract in participant.Value.Contracts)
                {
                    if (!allContracts.TryGetValue(contract, out var declarations))
                        allContracts.Add(contract, declarations = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));
                    declarations.Add(network.Symbol);
                }
            }

            _validateNetwork(context, network, participants);
        }

        foreach (var membership in participantNetworks.Where(pair => pair.Value.Count > 1))
            _report(context, _multipleNetworks, membership.Key, membership.Key.ToDisplayString());
        foreach (var contract in allContracts.Where(pair => pair.Value.Count > 1))
            _report(context, _crossNetworkContract, contract.Key, contract.Key.ToDisplayString());

        _validateContractNames(context, allContracts.Keys);
        _emitMetadata(context, networks);
        foreach (var network in networks)
            _emitNetwork(context, network, compilation);

        foreach (var participant in participantSymbols
            .OrderBy(symbol => symbol.ToDisplayString(), StringComparer.Ordinal))
        {
            var model = _readParticipant(participant);
            if (model is not null)
                _emitParticipant(context, model.Value, compilation);
        }
    }

    private static void _validateNetwork(
        SourceProductionContext context,
        Network network,
        IReadOnlyList<Participant> participants)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var processors = new Dictionary<INamedTypeSymbol, List<Participant>>(SymbolEqualityComparer.Default);
        var publishers = new Dictionary<INamedTypeSymbol, List<Participant>>(SymbolEqualityComparer.Default);

        foreach (var participant in participants)
        {
            if (!identities.Add(participant.Identity))
                _report(context, _duplicateIdentity, participant.Symbol, network.Name, participant.Identity);
            if (!_isLogicalName(participant.Identity))
                _report(context, _invalidIdentity, participant.Symbol, participant.Symbol.ToDisplayString(), participant.Identity);
            if (participant.Identity == "outbox-processor" || participant.Identity.EndsWith("-poison", StringComparison.Ordinal))
                _report(context, _reservedIdentity, participant.Symbol, participant.Symbol.ToDisplayString(), participant.Identity);
            if (participant.Contracts.Length > 0 && !participant.Serializers.Contains(participant.DefaultSerializer))
                _report(context, _defaultSerializer, participant.Symbol, participant.Identity, participant.DefaultSerializer);
            if (participant.Retry is not null
                && participant.Retry.Value.MaximumDeliveryCount < (participant.Retry.Value.SecondLevelRetriesEnabled ? 2 : 1))
                _report(context, _invalidRetry, participant.Symbol, participant.Identity, participant.Retry.Value.SecondLevelRetriesEnabled ? 2 : 1);

            if (participant.Processes.Length > 0 || participant.Subscribes.Length > 0)
                _requireCapability(context, network, participant, "SendReceive", _sendReceive);
            if (participant.Publishes.Length > 0 || participant.Subscribes.Length > 0)
                _requireCapability(context, network, participant, "PubSub", _pubSub);

            foreach (var contract in participant.Processes)
                _add(processors, contract, participant);
            foreach (var contract in participant.Publishes)
                _add(publishers, contract, participant);
        }

        foreach (var processor in processors)
            MessagingContractTopologyValidator.Validate(
                (descriptor, location, arguments) =>
                    context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments)),
                processor.Key,
                processor.Value[0].Symbol,
                processor.Value[0].DefaultSerializer);
        foreach (var publisher in publishers)
            MessagingContractTopologyValidator.Validate(
                (descriptor, location, arguments) =>
                    context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments)),
                publisher.Key,
                publisher.Value[0].Symbol,
                publisher.Value[0].DefaultSerializer);

        foreach (var processor in processors)
        {
            if (processor.Value.Count > 1)
                _report(context, _multipleProcessor, processor.Key, _contractName(processor.Key), network.Name);
        }
        foreach (var publisher in publishers)
        {
            if (publisher.Value.Count > 1)
                _report(context, _multiplePublisher, publisher.Key, _contractName(publisher.Key), network.Name);
        }

        foreach (var participant in participants)
        {
            foreach (var subscription in participant.Subscribes)
            {
                if (!publishers.TryGetValue(subscription, out var eventPublishers))
                {
                    _report(context, _unsatisfiableSubscription, participant.Symbol, participant.Identity, _contractName(subscription), network.Name);
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
                        _contractName(subscription),
                        _protocolName(eventPublishers[0].DefaultSerializer));
            }
        }

        foreach (var contract in participants.SelectMany(participant => participant.Contracts)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>())
        {
            var hasProcessor = processors.ContainsKey(contract);
            var hasPublisher = publishers.ContainsKey(contract);
            var attribute = _contractAttributes(contract);
            if (attribute.Message is not null && attribute.Event is not null)
                _report(context, _dualContract, contract, contract.ToDisplayString());
            if (attribute.Event is not null && !_isEventShape(contract))
                _report(context, _invalidEventShape, contract, contract.ToDisplayString());
            if (!hasProcessor && !hasPublisher)
                _report(context, _unwiredContract, contract, _contractName(contract), network.Name);
        }
    }

    private static void _requireCapability(
        SourceProductionContext context,
        Network network,
        Participant participant,
        string name,
        int capability)
    {
        if ((network.Requires & capability) == 0)
            _report(context, _missingCapability, participant.Symbol, participant.Identity, name, network.Name);
    }

    private static void _validateContractNames(
        SourceProductionContext context,
        IEnumerable<INamedTypeSymbol> contracts)
    {
        var currentNames = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var contract in contracts.OrderBy(symbol => symbol.ToDisplayString(), StringComparer.Ordinal))
        {
            var attributes = _contractAttributes(contract);
            var current = attributes.Message is not null || attributes.Event is not null
                ? attributes.Name ?? _defaultContractName(contract)
                : _contractName(contract);
            if (attributes.Name is not null && !_isNormalized(attributes.Name))
                _report(context, _nonNormalizedName, contract, contract.ToDisplayString(), attributes.Name);
            foreach (var alias in attributes.FormerNames)
            {
                if (!_isNormalized(alias))
                    _report(context, _nonNormalizedName, contract, contract.ToDisplayString(), alias);
                if (!aliases.TryAdd(alias, contract))
                    _report(context, _duplicateAlias, contract, alias);
            }
            if (!currentNames.TryAdd(current, contract))
                _report(context, _duplicateName, contract, current);
        }

        foreach (var alias in aliases)
        {
            if (currentNames.ContainsKey(alias.Key))
                _report(context, _aliasCollision, alias.Value, alias.Key);
        }
    }

    private static Network _readNetwork(INamedTypeSymbol symbol)
    {
        var attribute = symbol.GetAttributes().First(attribute =>
            attribute.AttributeClass?.ToDisplayString() == _networkAttribute);
        var members = _types(attribute, "Members");
        return new Network(
            symbol,
            symbol.ToDisplayString(),
            members,
            _enum(attribute, "Requires"),
            _optionalInt(attribute, "MaximumDecompressedPayloadBytes"),
            _optionalInt(attribute, "DataBusMaximumAttachmentBytes"),
            _optionalInt(attribute, "MaximumSchedulingDelaySeconds"),
            _optionalInt(attribute, "ResourceLifecycle"),
            _string(attribute, "ConnectionConfigurationKey"),
            _string(attribute, "ManagedIdentityConfigurationKey"));
    }

    private static Participant? _readParticipant(INamedTypeSymbol symbol)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(attribute =>
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
            symbol,
            identity,
            processes,
            publishes,
            subscribes,
            serializers,
            _enum(attribute, "DefaultSerializer"),
            compression,
            compressionMinimumSizeBytes,
            retryType,
            retry,
            processes.Concat(publishes).Concat(subscribes)
                .Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToImmutableArray());
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
            .FirstOrDefault(candidate => candidate.IsConst && candidate.ConstantValue is int);
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
                    .SelectMany(accessor => accessor.Body?.Statements ?? Enumerable.Empty<Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax>())
                    .OfType<ReturnStatementSyntax>()
                    .Select(statement => statement.Expression)
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
            .FirstOrDefault(candidate => candidate.IsConst && candidate.ConstantValue is bool);
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
                    .SelectMany(accessor => accessor.Body?.Statements ?? Enumerable.Empty<Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax>())
                    .OfType<ReturnStatementSyntax>()
                    .Select(statement => statement.Expression)
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
        var message = symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == _messageAttribute);
        var @event = symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == _eventAttribute);
        var source = message ?? @event;
        return (message, @event, _string(source, "Name"), _strings(source, "FormerNames"));
    }

    private static bool _isEventShape(INamedTypeSymbol symbol)
    {
        return symbol.AllInterfaces.Any(@interface =>
        {
            if (@interface.OriginalDefinition.ContainingNamespace.ToDisplayString() != _requestNamespace)
                return false;

            var metadataName = @interface.OriginalDefinition.MetadataName;
            var isCommand = metadataName == _commandInterfaceName && @interface.TypeArguments.Length == 1;
            var isRequest = metadataName == _requestInterfaceName && @interface.TypeArguments.Length == 2;
            return (isCommand || isRequest)
                && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], symbol);
        });
    }

    private static string _contractName(INamedTypeSymbol symbol)
    {
        var attributes = _contractAttributes(symbol);
        return attributes.Name ?? _defaultContractName(symbol);
    }

    private static string _defaultContractName(INamedTypeSymbol symbol)
    {
        var group = symbol.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == _apiGroupAttribute)
            ?.ConstructorArguments.FirstOrDefault().Value as string ?? "Ark";
        return _normalizeLogical(group) + "." + _normalizeLogical(symbol.Name);
    }

    private static string _normalizeIdentity(string value)
    {
        return string.Join("-", _words(value).Select(word => word.ToLowerInvariant()));
    }

    private static string _normalizeSnake(string value)
    {
        return string.Join("_", value.Split('.').SelectMany(_words).Select(word => word.ToLowerInvariant()));
    }

    private static string _normalizeLogical(string value)
    {
        return string.Join(".", value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => string.Join("-", _words(segment).Select(word => word.ToLowerInvariant()))));
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
        SourceProductionContext context,
        Network network,
        Compilation compilation)
    {
        if (!_validateDeclaringType(context, network.Symbol, "MessagingNetwork", requireStatic: true))
            return;

        var participants = network.MemberSymbols
            .Select(_readParticipant)
            .Where(static participant => participant is not null)
            .Select(static participant => participant!.Value)
            .ToArray();
        var processors = participants
            .SelectMany(participant => participant.Processes.Select(contract => (contract, participant)))
            .GroupBy(item => item.contract, SymbolEqualityComparer.Default)
            .ToDictionary(group => group.Key, group => group.First().participant, SymbolEqualityComparer.Default);
        var publishers = participants
            .SelectMany(participant => participant.Publishes.Select(contract => (contract, participant)))
            .GroupBy(item => item.contract, SymbolEqualityComparer.Default)
            .ToDictionary(group => group.Key, group => group.First().participant, SymbolEqualityComparer.Default);
        var contracts = processors.Keys
            .Concat(publishers.Keys)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .OrderBy(contract => contract.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();
        var name = network.Symbol.Name;
        var source = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("using global::System;")
            .AppendLine("using global::System.Collections.Generic;")
            .AppendLine("using global::System.Collections.Frozen;");
        if (!network.Symbol.ContainingNamespace.IsGlobalNamespace)
        {
            source.Append("namespace ").Append(network.Symbol.ContainingNamespace.ToDisplayString()).AppendLine(";")
                .AppendLine();
        }

        source.AppendLine("[global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append(_accessibility(network.Symbol)).Append(" static partial class ").Append(name)
            .AppendLine("{");
        source
            .AppendLine("    /// <summary>Gets the resolved identity of this messaging network.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public static string NetworkIdentity => \"").Append(_escape(network.Name)).AppendLine("\";")
            .AppendLine();

        if (compilation.GetTypeByMetadataName(_networkOptions) is not null)
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
            foreach (var member in network.MemberSymbols)
                source.Append("                    typeof(").Append(_typeName(member)).AppendLine("),");
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
            if (network.ConnectionConfigurationKey is not null)
                source.Append("                ConnectionConfigurationKey = \"").Append(_escape(network.ConnectionConfigurationKey)).AppendLine("\",");
            if (network.ManagedIdentityConfigurationKey is not null)
                source.Append("                ManagedIdentityConfigurationKey = \"").Append(_escape(network.ManagedIdentityConfigurationKey)).AppendLine("\",");
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
                : publishers[contract].Identity + "-" + _contractName(contract);
            source.Append("            [typeof(").Append(_typeName(contract)).Append(")] = \"")
                .Append(_escape(destination)).AppendLine("\",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, string> _processors =")
            .AppendLine("        new Dictionary<Type, string>")
            .AppendLine("        {");
        foreach (var contract in processors.Keys.Cast<INamedTypeSymbol>()
            .OrderBy(contract => contract.ToDisplayString(), StringComparer.Ordinal))
        {
            source.Append("            [typeof(").Append(_typeName(contract)).Append(")] = \"")
               .Append(_escape(processors[contract].Identity)).AppendLine("\",");
        }

        source.AppendLine("        }.ToFrozenDictionary();")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    private static readonly FrozenDictionary<Type, string> _publishers =")
            .AppendLine("        new Dictionary<Type, string>")
            .AppendLine("        {");
        foreach (var contract in publishers.Keys.Cast<INamedTypeSymbol>()
            .OrderBy(contract => contract.ToDisplayString(), StringComparer.Ordinal))
        {
            source.Append("            [typeof(").Append(_typeName(contract)).Append(")] = \"")
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
            source.Append("            [typeof(").Append(_typeName(contract)).Append(")] = global::Ark.Tools.MediatorFramework.SerializationProtocol.")
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
            source.Append("            [typeof(").Append(_typeName(contract)).Append(")] = \"")
                .Append(_escape(_contractName(contract))).AppendLine("\",");
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

        if (compilation.GetTypeByMetadataName(_contractRegistry) is not null)
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
            _safeIdentifier(network.Symbol.ToDisplayString()) + "_" + _stableHash(network.Symbol.ToDisplayString()) + ".Registry.g.cs",
            source.ToString());
    }

    private static void _emitParticipant(
        SourceProductionContext context,
        Participant participant,
        Compilation compilation)
    {
        if (!_validateDeclaringType(context, participant.Symbol, "MessagingParticipant"))
            return;

        var source = new StringBuilder()
            .AppendLine("// <auto-generated />");
        if (!participant.Symbol.ContainingNamespace.IsGlobalNamespace)
        {
            source.Append("namespace ").Append(participant.Symbol.ContainingNamespace.ToDisplayString()).AppendLine(";")
                .AppendLine();
        }

        var commandInterface = compilation.GetTypeByMetadataName(_commandInterface);
        var canEmitBinder = compilation.GetTypeByMetadataName(_payloadReader) is not null
            && compilation.GetTypeByMetadataName(_commandProcessor) is not null
            && compilation.GetTypeByMetadataName(_failFastException) is not null
            && compilation.GetTypeByMetadataName(_failFastReason) is not null
            && commandInterface is not null;
        var canEmitStreamBinder = canEmitBinder
            && compilation.GetTypeByMetadataName(_streamPayloadReader) is not null
            && compilation.GetTypeByMetadataName(_codec) is not null;
        var canEmitFailedBinder = canEmitBinder
            && compilation.GetTypeByMetadataName(_failedMessage) is not null
            && compilation.GetTypeByMetadataName(_exceptionInfo) is not null;
        var canEmitPayloadSender = compilation.GetTypeByMetadataName(_payloadSender) is not null
            && compilation.GetTypeByMetadataName(_dataBus) is not null
            && compilation.GetTypeByMetadataName(_networkOptions) is not null;
        var canEmitDescriptor = compilation.GetTypeByMetadataName(_participantDescriptor) is not null
            && compilation.GetTypeByMetadataName(_contractRegistry) is not null;

        source.AppendLine("[global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append(_accessibility(participant.Symbol)).Append(" partial class ").Append(participant.Symbol.Name).AppendLine()
            .AppendLine("{")
            .AppendLine("    /// <summary>Gets the resolved identity of this messaging participant.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public const string Identity = \"").Append(_escape(participant.Identity)).AppendLine("\";")
            .AppendLine("    /// <summary>Gets the sender-side compression algorithm.</summary>")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append("    public const global::Ark.Tools.MediatorFramework.CompressionAlgorithm Compression = global::Ark.Tools.MediatorFramework.CompressionAlgorithm.")
            .Append(_compressionName(compilation, participant.Compression)).AppendLine(";")
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
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .Where(contract => !canEmitBinder || _implementsCommand(contract, commandInterface!))
            .OrderBy(contract => contract.ToDisplayString(), StringComparer.Ordinal)
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
                .AppendLine("        global::System.Threading.CancellationToken ctk)")
                .AppendLine("    {")
                .AppendLine("        switch (logicalName)")
                .AppendLine("        {");
            foreach (var contract in contracts)
            {
                var names = new[] { _contractName(contract) }
                    .Concat(_contractAttributes(contract).FormerNames)
                    .Distinct(StringComparer.Ordinal);
                foreach (var wireName in names)
                    source.Append("            case \"").Append(_escape(wireName)).AppendLine("\":");
                source.Append("                var message = payload.Deserialize<").Append(_typeName(contract)).AppendLine(">();")
                    .Append("                await processor.ExecuteAsync<").Append(_typeName(contract)).AppendLine(">(message, ctk).ConfigureAwait(false);")
                    .AppendLine("                break;");
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
                    .AppendLine("        global::System.Threading.CancellationToken ctk)")
                    .AppendLine("    {")
                    .AppendLine("        await using var reader = new global::Ark.Tools.MediatorFramework.Messaging.MessagingStreamPayloadReader(payload, codec);")
                    .AppendLine("        await DispatchAsync(logicalName, reader, processor, ctk).ConfigureAwait(false);")
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
                    var names = new[] { _contractName(contract) }
                        .Concat(_contractAttributes(contract).FormerNames)
                        .Distinct(StringComparer.Ordinal);
                    foreach (var wireName in names)
                        source.Append("            case \"").Append(_escape(wireName)).AppendLine("\":");
                    source.Append("                var message = payload.Deserialize<").Append(_typeName(contract)).AppendLine(">();")
                        .Append("                var failed = new global::Ark.Tools.MediatorFramework.MessagingFailed<")
                        .Append(_typeName(contract)).AppendLine(">(message, deliveryCount, new[] { error });")
                        .Append("                await processor.ExecuteAsync<global::Ark.Tools.MediatorFramework.MessagingFailed<")
                        .Append(_typeName(contract)).AppendLine(">>(failed, ctk).ConfigureAwait(false);")
                        .AppendLine("                return;");
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
                .Append("            typeof(").Append(_typeName(participant.Symbol)).AppendLine("),")
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
                .Append(participant.RetryType is null
                    ? "global::Ark.Tools.MediatorFramework.Messaging.MessagingDefaultRetryPolicy.Instance"
                    : "new " + _typeName(participant.RetryType) + "()")
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
                    source.Append("                typeof(global::Ark.Tools.Solid.ICommandHandler<")
                        .Append(_typeName(contract)).AppendLine(">),");
                    if (participant.Retry?.SecondLevelRetriesEnabled == true)
                    {
                        source.Append("                typeof(global::Ark.Tools.Solid.ICommandHandler<global::Ark.Tools.MediatorFramework.MessagingFailed<")
                            .Append(_typeName(contract)).AppendLine(">>),");
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
                    .Append(_escape(participant.Identity + "-" + _contractName(contract)))
                    .Append("\", \"").Append(_escape(participant.Identity)).AppendLine("\"),");
            }
            source.AppendLine("            });");
            source.AppendLine("    }");
        }

        source.AppendLine("}");
        context.AddSource(
            _safeIdentifier(participant.Symbol.ToDisplayString()) + "_" + _stableHash(participant.Symbol.ToDisplayString()) + ".Participant.g.cs",
            source.ToString());
    }

    private static bool _validateDeclaringType(
        SourceProductionContext context,
        INamedTypeSymbol symbol,
        string attributeName,
        bool requireStatic = false)
    {
        #pragma warning disable MA0040, MA0045
        var isPartial = symbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
        #pragma warning restore MA0040, MA0045
        if (symbol.ContainingType is not null
            || symbol.Arity != 0
            || !isPartial)
        {
            _report(context, _nonPartialDeclaringType, symbol, symbol.ToDisplayString(), attributeName);
            return false;
        }
        if (requireStatic && !symbol.IsStatic)
        {
            _report(context, _nonStaticNetwork, symbol, symbol.ToDisplayString());
            return false;
        }
        return true;
    }

    private static bool _implementsCommand(INamedTypeSymbol contract, INamedTypeSymbol commandInterface)
    {
        return contract.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, commandInterface));
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

    private static void _emitMetadata(SourceProductionContext context, IReadOnlyList<Network> networks)
    {
        var source = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("namespace Ark.Tools.MediatorFramework.Generated;");
        foreach (var network in networks)
        {
            var displayName = network.Symbol.ToDisplayString();
            var name = _safeIdentifier(displayName) + "_" + _stableHash(displayName) + "MessagingDescriptor";
            source.Append("internal static class ").Append(name).AppendLine()
                .AppendLine("{")
                .Append("    internal const string Network = \"").Append(_escape(network.Name)).AppendLine("\";")
                .Append("    internal static readonly string[] Members = new string[] { ")
                .Append(string.Join(", ", network.MemberSymbols.Select(member => "\"" + _escape(member.ToDisplayString()) + "\"")))
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

    private static void _add(
        IDictionary<INamedTypeSymbol, List<Participant>> map,
        INamedTypeSymbol contract,
        Participant participant)
    {
        if (!map.TryGetValue(contract, out var values))
            map.Add(contract, values = new List<Participant>());
        values.Add(participant);
    }

    private static void _report(
        SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        ISymbol symbol,
        params object[] arguments)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            descriptor, symbol.Locations.FirstOrDefault() ?? Location.None, arguments));
    }

    private static ImmutableArray<INamedTypeSymbol> _types(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        if (value.Kind != TypedConstantKind.Array)
            return ImmutableArray<INamedTypeSymbol>.Empty;
        return value.Values
            .Where(item => item.Value is INamedTypeSymbol)
            .Select(item => (INamedTypeSymbol)item.Value!)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> _strings(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return ImmutableArray<string>.Empty;
        var value = _named(attribute, name);
        if (value.Kind != TypedConstantKind.Array)
            return ImmutableArray<string>.Empty;
        return value.Values.Where(item => item.Value is string).Select(item => (string)item.Value!).ToImmutableArray();
    }

    private static ImmutableArray<int> _enums(AttributeData attribute, string name)
    {
        var value = _named(attribute, name);
        if (value.Kind != TypedConstantKind.Array)
            return ImmutableArray<int>.Empty;
        return value.Values.Select(item => (int)item.Value!).ToImmutableArray();
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

    private readonly struct Network
    {
        public Network(
            INamedTypeSymbol symbol,
            string name,
            ImmutableArray<INamedTypeSymbol> memberSymbols,
            int requires,
            int? maximumDecompressedPayloadBytes,
            int? dataBusMaximumAttachmentBytes,
            int? maximumSchedulingDelaySeconds,
            int? resourceLifecycle,
            string? connectionConfigurationKey,
            string? managedIdentityConfigurationKey)
        {
            Symbol = symbol;
            Name = name;
            MemberSymbols = memberSymbols;
            Requires = requires;
            MaximumDecompressedPayloadBytes = maximumDecompressedPayloadBytes;
            DataBusMaximumAttachmentBytes = dataBusMaximumAttachmentBytes;
            MaximumSchedulingDelaySeconds = maximumSchedulingDelaySeconds;
            ResourceLifecycle = resourceLifecycle;
            ConnectionConfigurationKey = connectionConfigurationKey;
            ManagedIdentityConfigurationKey = managedIdentityConfigurationKey;
        }

        public INamedTypeSymbol Symbol { get; }
        public string Name { get; }
        public ImmutableArray<INamedTypeSymbol> MemberSymbols { get; }
        public int Requires { get; }
        public int? MaximumDecompressedPayloadBytes { get; }
        public int? DataBusMaximumAttachmentBytes { get; }
        public int? MaximumSchedulingDelaySeconds { get; }
        public int? ResourceLifecycle { get; }
        public string? ConnectionConfigurationKey { get; }
        public string? ManagedIdentityConfigurationKey { get; }
    }

    private readonly struct Participant
    {
        public Participant(
            INamedTypeSymbol symbol,
            string identity,
            ImmutableArray<INamedTypeSymbol> processes,
            ImmutableArray<INamedTypeSymbol> publishes,
            ImmutableArray<INamedTypeSymbol> subscribes,
            ImmutableArray<int> serializers,
            int defaultSerializer,
            int compression,
            int compressionMinimumSizeBytes,
            INamedTypeSymbol? retryType,
            RetryPolicy? retry,
            ImmutableArray<INamedTypeSymbol> contracts)
        {
            Symbol = symbol;
            Identity = identity;
            Processes = processes;
            Publishes = publishes;
            Subscribes = subscribes;
            Serializers = serializers;
            DefaultSerializer = defaultSerializer;
            Compression = compression;
            CompressionMinimumSizeBytes = compressionMinimumSizeBytes;
            RetryType = retryType;
            Retry = retry;
            Contracts = contracts;
        }

        public INamedTypeSymbol Symbol { get; }
        public string Identity { get; }
        public ImmutableArray<INamedTypeSymbol> Processes { get; }
        public ImmutableArray<INamedTypeSymbol> Publishes { get; }
        public ImmutableArray<INamedTypeSymbol> Subscribes { get; }
        public ImmutableArray<int> Serializers { get; }
        public int DefaultSerializer { get; }
        public int Compression { get; }
        public int CompressionMinimumSizeBytes { get; }
        public INamedTypeSymbol? RetryType { get; }
        public RetryPolicy? Retry { get; }
        public ImmutableArray<INamedTypeSymbol> Contracts { get; }
    }

    private readonly struct RetryPolicy
    {
        public RetryPolicy(int maximumDeliveryCount, bool secondLevelRetriesEnabled)
        {
            MaximumDeliveryCount = maximumDeliveryCount;
            SecondLevelRetriesEnabled = secondLevelRetriesEnabled;
        }

        public int MaximumDeliveryCount { get; }
        public bool SecondLevelRetriesEnabled { get; }
    }
}
