// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Generators;

/// <summary>Generates Azure Functions messaging triggers and desired-resource manifests.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class MessagingFunctionsGenerator : IIncrementalGenerator
{
    private const string _hostAttribute =
        "Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHostAttribute";
    private const string _participantAttribute =
        "Ark.Tools.MediatorFramework.MessagingParticipantAttribute";
    private const string _networkAttribute =
        "Ark.Tools.MediatorFramework.MessagingNetworkAttribute";
    private const string _messageAttribute =
        "Ark.Tools.MediatorFramework.MessageAttribute";
    private const string _eventAttribute =
        "Ark.Tools.MediatorFramework.EventAttribute";
    private const int _serviceBusBinding = 0;

    private static readonly DiagnosticDescriptor _multipleHosts = _rule(
        "ARKMF033",
        "Multiple Functions messaging hosts",
        "An Azure Functions app can bind exactly one messaging participant",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidParticipant = _rule(
        "ARKMF034",
        "Invalid Functions messaging participant",
        "Type '{0}' is not marked with MessagingParticipant",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _missingNetwork = _rule(
        "ARKMF035",
        "Functions messaging participant has no network",
        "Participant '{0}' is not listed by a messaging network",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _multipleNetworks = _rule(
        "ARKMF036",
        "Functions messaging participant has multiple networks",
        "Participant '{0}' is listed by more than one messaging network",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _senderOnly = _rule(
        "ARKMF037",
        "Functions messaging participant is sender-only",
        "Participant '{0}' consumes no contracts, so no receive trigger is generated",
        DiagnosticSeverity.Info);
    private static readonly DiagnosticDescriptor _unsupportedBinding = _rule(
        "ARKMF038",
        "Functions messaging trigger binding is not implemented",
        "Trigger binding value '{0}' is not supported by this generator version",
        DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor _invalidSubscription = _rule(
        "ARKMF039",
        "Functions messaging subscription has no publisher",
        "Subscribed event '{0}' does not have exactly one publisher in network '{1}'",
        DiagnosticSeverity.Error);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hosts = context.SyntaxProvider.ForAttributeWithMetadataName(
                _hostAttribute,
                static (_, _) => true,
                static (attributeContext, _) => _readHosts(attributeContext))
            .Collect();

        context.RegisterSourceOutput(
            hosts,
            static (productionContext, hostGroups) =>
                _emit(productionContext, hostGroups.SelectMany(static group => group).ToImmutableArray()));
    }

    private static ImmutableArray<Host> _readHosts(GeneratorAttributeSyntaxContext context)
    {
        var hosts = ImmutableArray.CreateBuilder<Host>();
        foreach (var attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length < 2
                || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol participant
                || attribute.ConstructorArguments[1].Value is not int binding)
                continue;

            hosts.Add(new Host(
                participant,
                binding,
                _string(attribute, "ConnectionConfigurationKey"),
                _types(attribute, "IncomingSteps"),
                _types(attribute, "OutgoingSteps"),
                attribute.ApplicationSyntaxReference is { } syntax
                    ? Location.Create(syntax.SyntaxTree, syntax.Span)
                    : Location.None));
        }

        return hosts.ToImmutable();
    }

    private static void _emit(SourceProductionContext context, ImmutableArray<Host> hosts)
    {
        if (hosts.IsDefaultOrEmpty)
            return;
        if (hosts.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(_multipleHosts, hosts[0].Location));
            return;
        }

        var host = hosts[0];
        var participantAttribute = host.Participant.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == _participantAttribute);
        if (participantAttribute is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _invalidParticipant,
                host.Location,
                host.Participant.ToDisplayString()));
            return;
        }

        var networks = _allTypes(host.Participant.ContainingAssembly.GlobalNamespace)
            .Select(type => (Type: type, Attribute: type.GetAttributes().FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == _networkAttribute)))
            .Where(item => item.Attribute is not null
                && _types(item.Attribute, "Members").Any(member =>
                    SymbolEqualityComparer.Default.Equals(member, host.Participant)))
            .ToArray();
        if (networks.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _missingNetwork,
                host.Location,
                host.Participant.ToDisplayString()));
            return;
        }
        if (networks.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _multipleNetworks,
                host.Location,
                host.Participant.ToDisplayString()));
            return;
        }
        if (host.Binding != _serviceBusBinding)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _unsupportedBinding,
                host.Location,
                host.Binding.ToString(CultureInfo.InvariantCulture)));
            return;
        }

        var network = networks[0];
        var identity = _string(participantAttribute, "Identity")
            ?? _normalizeIdentity(host.Participant.Name.EndsWith("Participant", StringComparison.Ordinal)
                ? host.Participant.Name.Substring(0, host.Participant.Name.Length - "Participant".Length)
                : host.Participant.Name);
        var processes = _types(participantAttribute, "Processes");
        var subscribes = _types(participantAttribute, "Subscribes");
        var subscriptions = _createSubscriptions(
            context,
            network.Type,
            network.Attribute!,
            host.Participant,
            identity,
            subscribes);
        if (subscriptions is null)
            return;

        var connection = host.ConnectionConfigurationKey
            ?? network.Type.Name;
        var retryType = _type(participantAttribute, "Retry");
        var source = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("#nullable enable")
            .AppendLine("namespace Ark.Tools.MediatorFramework.AzureFunctions.Generated;")
            .AppendLine()
            .AppendLine("public static class ArkGeneratedMessagingFunctions")
            .AppendLine("{");

        _emitManifest(
            source,
            host,
            network.Type,
            identity,
            connection,
            retryType,
            subscriptions.Value);

        if (processes.IsDefaultOrEmpty && subscribes.IsDefaultOrEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _senderOnly,
                host.Location,
                host.Participant.ToDisplayString()));
        }
        else
        {
            _emitTrigger(source, identity, connection);
        }

        source.AppendLine("}");
        context.AddSource("ArkGeneratedMessagingFunctions.g.cs", source.ToString());
    }

    private static ImmutableArray<Subscription>? _createSubscriptions(
        SourceProductionContext context,
        INamedTypeSymbol network,
        AttributeData networkAttribute,
        INamedTypeSymbol participant,
        string participantIdentity,
        ImmutableArray<INamedTypeSymbol> subscribedEvents)
    {
        var members = _types(networkAttribute, "Members")
            .Select(member => (Type: member, Attribute: member.GetAttributes().FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == _participantAttribute)))
            .Where(static item => item.Attribute is not null)
            .ToArray();
        var subscriptions = ImmutableArray.CreateBuilder<Subscription>();
        foreach (var subscribedEvent in subscribedEvents
            .OrderBy(static type => type.ToDisplayString(), StringComparer.Ordinal))
        {
            var publishers = members.Where(item =>
                    _types(item.Attribute, "Publishes").Any(contract =>
                        SymbolEqualityComparer.Default.Equals(contract, subscribedEvent)))
                .ToArray();
            if (publishers.Length != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _invalidSubscription,
                    participant.Locations.FirstOrDefault() ?? Location.None,
                    subscribedEvent.ToDisplayString(),
                    network.ToDisplayString()));
                return null;
            }

            var publisherIdentity = _string(publishers[0].Attribute, "Identity")
                ?? _normalizeIdentity(publishers[0].Type.Name.EndsWith("Participant", StringComparison.Ordinal)
                    ? publishers[0].Type.Name.Substring(
                        0,
                        publishers[0].Type.Name.Length - "Participant".Length)
                    : publishers[0].Type.Name);
            var topic = publisherIdentity + "-" + _contractName(subscribedEvent);
            var prefixLength = Math.Min(participantIdentity.Length, 41);
            var subscriptionName = participantIdentity.Substring(0, prefixLength)
                + "-"
                + _stableHash(topic);
            subscriptions.Add(new Subscription(topic, subscriptionName, participantIdentity));
        }

        return subscriptions.ToImmutable();
    }

    private static void _emitManifest(
        StringBuilder source,
        Host host,
        INamedTypeSymbol network,
        string identity,
        string connection,
        INamedTypeSymbol? retryType,
        ImmutableArray<Subscription> subscriptions)
    {
        source.AppendLine("    /// <summary>Gets the deterministic desired-resource manifest for this messaging host.</summary>")
            .AppendLine("    public static global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsManifest Manifest { get; } =")
            .AppendLine("        new global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsManifest(")
            .Append("            typeof(").Append(_typeName(host.Participant)).AppendLine("),")
            .Append("            typeof(").Append(_typeName(network)).AppendLine("),")
            .AppendLine("            global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.ServiceBus,")
            .Append("            \"").Append(_escape(identity)).AppendLine("\",")
            .Append("            \"").Append(_escape(connection)).AppendLine("\",");

        if (retryType is null)
        {
            source.AppendLine("            1,")
                .AppendLine("            global::System.TimeSpan.FromMinutes(5),");
        }
        else
        {
            source.Append("            checked(new ").Append(_typeName(retryType))
                .AppendLine("().MaximumDeliveryCount")
                .Append("                * (new ").Append(_typeName(retryType))
                .AppendLine("().SecondLevelRetriesEnabled ? 2 : 1)),")
                .Append("            new ").Append(_typeName(retryType))
                .AppendLine("().MaximumHandlerDuration,");
        }

        source.AppendLine("            new global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsSubscription[]")
            .AppendLine("            {");
        foreach (var subscription in subscriptions)
        {
            source.AppendLine("                new global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsSubscription(")
                .Append("                    \"").Append(_escape(subscription.Topic)).AppendLine("\",")
                .Append("                    \"").Append(_escape(subscription.Name)).AppendLine("\",")
                .Append("                    \"").Append(_escape(subscription.ForwardToQueue)).AppendLine("\"),");
        }
        source.AppendLine("            },");
        _emitTypes(source, host.IncomingSteps);
        source.AppendLine(",");
        _emitTypes(source, host.OutgoingSteps);
        source.AppendLine(");")
            .AppendLine();
    }

    private static void _emitTypes(StringBuilder source, ImmutableArray<INamedTypeSymbol> types)
    {
        source.AppendLine("            new global::System.Type[]")
            .AppendLine("            {");
        foreach (var type in types.OrderBy(static type => type.ToDisplayString(), StringComparer.Ordinal))
            source.Append("                typeof(").Append(_typeName(type)).AppendLine("),");
        source.Append("            }");
    }

    private static void _emitTrigger(StringBuilder source, string identity, string connection)
    {
        var methodName = _functionName(identity);
        source.Append("    /// <summary>Receives the \"").Append(_escape(identity))
            .AppendLine("\" participant identity queue.</summary>")
            .Append("    [global::Microsoft.Azure.Functions.Worker.Function(\"")
            .Append(_escape(identity)).AppendLine("\")]")
            .Append("    public static async global::System.Threading.Tasks.Task ")
            .Append(methodName).AppendLine("(")
            .AppendLine("        [global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(")
            .Append("            \"").Append(_escape(identity)).AppendLine("\",")
            .Append("            Connection = \"").Append(_escape(connection)).AppendLine("\",")
            .AppendLine("            AutoCompleteMessages = false)]")
            .AppendLine("        global::Azure.Messaging.ServiceBus.ServiceBusReceivedMessage message,")
            .AppendLine("        global::Microsoft.Azure.Functions.Worker.ServiceBusMessageActions messageActions,")
            .AppendLine("        global::Microsoft.Azure.Functions.Worker.FunctionContext functionContext,")
            .AppendLine("        global::System.Threading.CancellationToken cancellationToken)")
            .AppendLine("    {")
            .AppendLine("        await global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsDispatcher")
            .AppendLine("            .DispatchAsync(message, messageActions, functionContext, cancellationToken)")
            .AppendLine("            .ConfigureAwait(false);")
            .AppendLine("    }");
    }

    private static IEnumerable<INamedTypeSymbol> _allTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in _nestedTypes(type))
                yield return nested;
        }
        foreach (var child in @namespace.GetNamespaceMembers())
        {
            foreach (var type in _allTypes(child))
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

    private static string? _string(AttributeData? attribute, string name)
    {
        return attribute?.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value
            as string;
    }

    private static string _contractName(INamedTypeSymbol contract)
    {
        var attribute = contract.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass?.ToDisplayString() is _messageAttribute or _eventAttribute);
        return _string(attribute, "Name") ?? _normalizeSnake(
            contract.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
    }

    private static string _normalizeIdentity(string value)
    {
        return string.Join("-", _words(value).Select(static word => word.ToLowerInvariant()));
    }

    private static string _normalizeSnake(string value)
    {
        return string.Join("_", value.Split('.')
            .SelectMany(_words)
            .Select(static word => word.ToLowerInvariant()));
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

    private static string _functionName(string identity)
    {
        var name = string.Concat(_words(identity).Select(word =>
            char.ToUpperInvariant(word[0]) + word.Substring(1)));
        return string.IsNullOrEmpty(name) ? "Messaging" : name;
    }

    private static string _typeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

    private static DiagnosticDescriptor _rule(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            message,
            "Ark.Tools.MediatorFramework",
            severity,
            isEnabledByDefault: true);
    }

    private readonly struct Host
    {
        public Host(
            INamedTypeSymbol participant,
            int binding,
            string? connectionConfigurationKey,
            ImmutableArray<INamedTypeSymbol> incomingSteps,
            ImmutableArray<INamedTypeSymbol> outgoingSteps,
            Location location)
        {
            Participant = participant;
            Binding = binding;
            ConnectionConfigurationKey = connectionConfigurationKey;
            IncomingSteps = incomingSteps;
            OutgoingSteps = outgoingSteps;
            Location = location;
        }

        public INamedTypeSymbol Participant { get; }

        public int Binding { get; }

        public string? ConnectionConfigurationKey { get; }

        public ImmutableArray<INamedTypeSymbol> IncomingSteps { get; }

        public ImmutableArray<INamedTypeSymbol> OutgoingSteps { get; }

        public Location Location { get; }
    }

    private readonly struct Subscription
    {
        public Subscription(string topic, string name, string forwardToQueue)
        {
            Topic = topic;
            Name = name;
            ForwardToQueue = forwardToQueue;
        }

        public string Topic { get; }

        public string Name { get; }

        public string ForwardToQueue { get; }
    }
}
