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
    private const string _requestNamespace = "Ark.Tools.Solid";
    private const string _commandInterface = "Ark.Tools.Solid.ICommand`1";
    private const string _payloadReader = "Ark.Tools.MediatorFramework.Messaging.IMessagingPayloadReader";
    private const string _commandProcessor = "Ark.Tools.Solid.ICommandProcessor";
    private const string _failFastException = "Ark.Tools.MediatorFramework.Messaging.MessagingFailFastException";
    private const string _failFastReason = "Ark.Tools.MediatorFramework.Messaging.MessagingFailFastReason";
    private const int _receive = 1;
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
        "Participant '{0}' does not support the default serializer of publisher '{1}' for event '{2}'", DiagnosticSeverity.Error);
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
        "Participant '{0}' has identity '{1}', which is not a valid portable queue name", DiagnosticSeverity.Error);
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
        "Event contract '{0}' must implement IRequest<TSelf, TResponse>", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _nonNormalizedName = _rule(
        "ARKMSG019", "Non-normalized contract name",
        "Contract '{0}' has explicit name or alias '{1}', which is not lowercase snake_case", DiagnosticSeverity.Error);
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
            _emitNetwork(context, network);

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
            if (!_isPortableName(participant.Identity))
                _report(context, _invalidIdentity, participant.Symbol, participant.Symbol.ToDisplayString(), participant.Identity);
            if (participant.Identity == "outbox-processor" || participant.Identity.EndsWith("-poison", StringComparison.Ordinal))
                _report(context, _reservedIdentity, participant.Symbol, participant.Symbol.ToDisplayString(), participant.Identity);
            if (participant.Contracts.Length > 0 && !participant.Serializers.Contains(participant.DefaultSerializer))
                _report(context, _defaultSerializer, participant.Symbol, participant.Identity, participant.DefaultSerializer);
            if (participant.Retry is not null
                && participant.Retry.Value.MaximumDeliveryCount < (participant.Retry.Value.SecondLevelRetriesEnabled ? 2 : 1))
                _report(context, _invalidRetry, participant.Symbol, participant.Identity, participant.Retry.Value.SecondLevelRetriesEnabled ? 2 : 1);

            if (participant.Processes.Length > 0 || participant.Subscribes.Length > 0)
                _requireCapability(context, network, participant, "Receive", _receive);
            if (participant.Publishes.Length > 0 || participant.Subscribes.Length > 0)
                _requireCapability(context, network, participant, "PubSub", _pubSub);

            foreach (var contract in participant.Processes)
                _add(processors, contract, participant);
            foreach (var contract in participant.Publishes)
                _add(publishers, contract, participant);
        }

        foreach (var processor in processors)
        {
            if (processor.Value.Count > 1)
                _report(context, _multipleProcessor, processor.Key, _contractName(processor.Key), network.Name);
        }
        foreach (var publisher in publishers)
        {
            if (publisher.Value.Count > 1)
                _report(context, _multiplePublisher, publisher.Key, _contractName(publisher.Key), network.Name);
            var topic = publisher.Value[0].Identity + "-" + _contractName(publisher.Key);
            if (topic.Length > 260)
                _report(context, _longTopic, publisher.Key, topic);
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
                    _report(context, _serializerMismatch, participant.Symbol, participant.Identity, eventPublishers[0].Identity, _contractName(subscription));
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
        return new Network(symbol, symbol.ToDisplayString(), members, _enum(attribute, "Requires"));
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
            @interface.OriginalDefinition.MetadataName == "IRequest`2"
            && @interface.OriginalDefinition.ContainingNamespace.ToDisplayString() == _requestNamespace
            && @interface.TypeArguments.Length == 2
            && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], symbol));
    }

    private static string _contractName(INamedTypeSymbol symbol)
    {
        var attributes = _contractAttributes(symbol);
        return attributes.Name ?? _defaultContractName(symbol);
    }

    private static string _defaultContractName(INamedTypeSymbol symbol)
    {
        var fullName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return _normalizeSnake(fullName);
    }

    private static string _normalizeIdentity(string value)
    {
        return string.Join("-", _words(value).Select(word => word.ToLowerInvariant()));
    }

    private static string _normalizeSnake(string value)
    {
        return string.Join("_", value.Split('.').SelectMany(_words).Select(word => word.ToLowerInvariant()));
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

    private static bool _isPortableName(string value)
    {
        if (value.Length < 3 || value.Length > 50 || value[0] == '-' || value[value.Length - 1] == '-')
            return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
                || (character == '-' && index > 0 && value[index - 1] == '-'))
                return false;
        }
        return true;
    }

    private static bool _isNormalized(string value)
    {
        if (value.Length == 0 || value[0] == '_' || value[value.Length - 1] == '_')
            return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')
                || (character == '_' && index > 0 && value[index - 1] == '_'))
                return false;
        }
        return true;
    }

    private static void _emitNetwork(
        SourceProductionContext context,
        Network network)
    {
        if (!_validateDeclaringType(context, network.Symbol, "MessagingNetwork"))
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
            .AppendLine("using global::System.Collections.Frozen;")
            .Append("namespace ").Append(network.Symbol.ContainingNamespace.ToDisplayString()).AppendLine(";")
            .AppendLine()
            .AppendLine("[global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append(_accessibility(network.Symbol)).Append(" partial class ").Append(name).AppendLine()
            .AppendLine("{")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    /// <summary>Gets the resolved identity of this messaging network.</summary>")
            .Append("    public static string NetworkIdentity => \"").Append(_escape(network.Name)).AppendLine("\";")
            .AppendLine()
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
        .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
        .AppendLine("    /// <summary>Gets the destination for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The declared contract type.</typeparam>")
            .AppendLine("    /// <returns>The participant queue or event topic.</returns>")
            .AppendLine("    public static string GetDestinationFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        if (_destinations.TryGetValue(typeof(T), out var destination))")
            .AppendLine("            return destination;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    /// <summary>Gets the owner-selected wire protocol for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The declared contract type.</typeparam>")
            .AppendLine("    /// <returns>The owner-selected serialization protocol.</returns>")
            .AppendLine("    public static global::Ark.Tools.MediatorFramework.SerializationProtocol GetWireProtocolFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        if (_wireProtocols.TryGetValue(typeof(T), out var protocol))")
            .AppendLine("            return protocol;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    /// <summary>Gets the current logical wire name for a declared contract.</summary>")
            .AppendLine("    /// <typeparam name=\"T\">The declared contract type.</typeparam>")
            .AppendLine("    /// <returns>The normalized logical contract name.</returns>")
            .AppendLine("    public static string GetLogicalNameFor<T>() where T : class")
            .AppendLine("    {")
            .AppendLine("        if (_logicalNames.TryGetValue(typeof(T), out var logicalName))")
            .AppendLine("            return logicalName;")
            .AppendLine("        throw new global::Ark.Tools.MediatorFramework.MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);")
            .AppendLine("    }")
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
            .AppendLine("// <auto-generated />")
            .AppendLine("namespace " + participant.Symbol.ContainingNamespace.ToDisplayString() + ";")
            .AppendLine()
            .AppendLine("[global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .Append(_accessibility(participant.Symbol)).Append(" partial class ").Append(participant.Symbol.Name).AppendLine()
            .AppendLine("{")
            .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
            .AppendLine("    /// <summary>Gets the resolved identity of this messaging participant.</summary>")
            .Append("    public const string Identity = \"").Append(_escape(participant.Identity)).AppendLine("\";");

        var commandInterface = compilation.GetTypeByMetadataName(_commandInterface);
        var canEmitBinder = compilation.GetTypeByMetadataName(_payloadReader) is not null
            && compilation.GetTypeByMetadataName(_commandProcessor) is not null
            && compilation.GetTypeByMetadataName(_failFastException) is not null
            && compilation.GetTypeByMetadataName(_failFastReason) is not null
            && commandInterface is not null;
        var contracts = participant.Processes
            .Concat(participant.Subscribes)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .OrderBy(contract => contract.ToDisplayString(), StringComparer.Ordinal)
            .Where(contract => !canEmitBinder || _implementsCommand(contract, commandInterface!))
            .ToArray();

        if (canEmitBinder && contracts.Length > 0)
        {
            source.AppendLine()
                .AppendLine("    [global::Ark.Tools.MediatorFramework.MessagingGeneratedSurface]")
                .AppendLine("    /// <summary>Dispatches a received contract by its current or former logical name.</summary>")
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
        }

        source.AppendLine("}");
        context.AddSource(
            _safeIdentifier(participant.Symbol.ToDisplayString()) + "_" + _stableHash(participant.Symbol.ToDisplayString()) + ".Participant.g.cs",
            source.ToString());
    }

    private static bool _validateDeclaringType(
        SourceProductionContext context,
        INamedTypeSymbol symbol,
        string attributeName)
    {
        var isPartial = symbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
        if (symbol.ContainingType is not null || symbol.Arity != 0 || !isPartial)
        {
            _report(context, _nonPartialDeclaringType, symbol, symbol.ToDisplayString(), attributeName);
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
        public Network(INamedTypeSymbol symbol, string name, ImmutableArray<INamedTypeSymbol> memberSymbols, int requires)
        {
            Symbol = symbol;
            Name = name;
            MemberSymbols = memberSymbols;
            Requires = requires;
        }

        public INamedTypeSymbol Symbol { get; }
        public string Name { get; }
        public ImmutableArray<INamedTypeSymbol> MemberSymbols { get; }
        public int Requires { get; }
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
