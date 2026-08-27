// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
    private const int _storageQueueBinding = 1;

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
    private static readonly DiagnosticDescriptor _hostJsonNotInspectable = _rule(
        "ARKMF040",
        "Storage Queue host settings are not inspectable",
        "Add host.json to AdditionalFiles so Storage Queue messaging settings can be validated",
        DiagnosticSeverity.Info);
    private static readonly DiagnosticDescriptor _invalidMessageEncoding = _rule(
        "ARKMF041",
        "Invalid Storage Queue message encoding",
        "host.json extensions.queues.messageEncoding must be the literal 'none'",
        DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor _invalidMaximumDequeueCount = _rule(
        "ARKMF042",
        "Invalid Storage Queue maximum dequeue count",
        "host.json extensions.queues.maxDequeueCount must be a positive integer",
        DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor _invalidVisibilityTimeout = _rule(
        "ARKMF043",
        "Invalid Storage Queue visibility timeout",
        "host.json extensions.queues.visibilityTimeout must be a positive TimeSpan",
        DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor _missingStorageQueueRetry = _rule(
        "ARKMF044",
        "Storage Queue consumer has no retry policy",
        "Storage Queue participant '{0}' must declare a retry policy with a positive RetryDelay",
        DiagnosticSeverity.Error);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hosts = context.SyntaxProvider.ForAttributeWithMetadataName(
                _hostAttribute,
                static (_, _) => true,
                static (attributeContext, _) => _readHosts(attributeContext))
            .Collect();
        var hostJson = context.AdditionalTextsProvider
            .Where(static text => string.Equals(
                Path.GetFileName(text.Path),
                "host.json",
                StringComparison.OrdinalIgnoreCase))
            .Select(static (text, cancellationToken) =>
                text.GetText(cancellationToken)?.ToString())
            .Collect();

        context.RegisterSourceOutput(
            hosts.Combine(hostJson),
            static (productionContext, input) =>
                _emit(
                    productionContext,
                    input.Left.SelectMany(static group => group).ToImmutableArray(),
                    input.Right));
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
                _bool(attribute, "StrictStorageQueueHostSettings"),
                attribute.ApplicationSyntaxReference is { } syntax
                    ? Location.Create(syntax.SyntaxTree, syntax.Span)
                    : Location.None));
        }

        return hosts.ToImmutable();
    }

    private static void _emit(
        SourceProductionContext context,
        ImmutableArray<Host> hosts,
        ImmutableArray<string?> hostJson)
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
        if (host.Binding is not (_serviceBusBinding or _storageQueueBinding))
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
        var retryType = _type(participantAttribute, "Retry");
        if (host.Binding == _storageQueueBinding
            && (!processes.IsDefaultOrEmpty || !subscribes.IsDefaultOrEmpty))
        {
            if (retryType is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _missingStorageQueueRetry,
                    host.Location,
                    host.Participant.ToDisplayString()));
                return;
            }

            _validateHostJson(context, host, hostJson);
        }
        var subscriptions = _createSubscriptions(
            context,
            network.Type,
            network.Attribute!,
            host.Participant,
            identity,
            subscribes);
        if (subscriptions is null)
            return;
        var topics = _createTopics(network.Attribute!);
        var desiredTopics = topics.Where(topic =>
                string.Equals(topic.OwnerIdentity, identity, StringComparison.Ordinal)
                || subscribes.Any(contract =>
                    SymbolEqualityComparer.Default.Equals(contract, topic.Contract)))
            .ToImmutableArray();

        var connection = host.ConnectionConfigurationKey
            ?? network.Type.Name;
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
            subscriptions.Value,
            desiredTopics,
            topics,
            !processes.IsDefaultOrEmpty || !subscribes.IsDefaultOrEmpty,
            _int(network.Attribute!, "ResourceLifecycle"));

        if (processes.IsDefaultOrEmpty && subscribes.IsDefaultOrEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _senderOnly,
                host.Location,
                host.Participant.ToDisplayString()));
        }
        else
        {
            _emitTrigger(source, host.Binding, identity, connection);
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
            subscriptions.Add(new Subscription(topic, participantIdentity, participantIdentity));
        }

        return subscriptions.ToImmutable();
    }

    private static ImmutableArray<Topic> _createTopics(AttributeData networkAttribute)
    {
        var topics = ImmutableArray.CreateBuilder<Topic>();
        foreach (var member in _types(networkAttribute, "Members"))
        {
            var participant = member.GetAttributes().FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == _participantAttribute);
            if (participant is null)
                continue;
            var ownerIdentity = _string(participant, "Identity")
                ?? _normalizeIdentity(member.Name.EndsWith("Participant", StringComparison.Ordinal)
                    ? member.Name.Substring(0, member.Name.Length - "Participant".Length)
                    : member.Name);
            foreach (var contract in _types(participant, "Publishes"))
            {
                topics.Add(new Topic(
                    contract,
                    ownerIdentity + "-" + _contractName(contract),
                    ownerIdentity));
            }
        }

        return topics.ToImmutable();
    }

    private static void _emitManifest(
        StringBuilder source,
        Host host,
        INamedTypeSymbol network,
        string identity,
        string connection,
        INamedTypeSymbol? retryType,
        ImmutableArray<Subscription> subscriptions,
        ImmutableArray<Topic> desiredTopics,
        ImmutableArray<Topic> knownTopics,
        bool hasIdentityQueue,
        int resourceLifecycle)
    {
        source.AppendLine("    /// <summary>Gets the deterministic desired-resource manifest for this messaging host.</summary>")
            .AppendLine("    public static global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsManifest Manifest { get; } =")
            .AppendLine("        new global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsManifest(")
            .Append("            typeof(").Append(_typeName(host.Participant)).AppendLine("),")
            .Append("            typeof(").Append(_typeName(network)).AppendLine("),")
            .Append("            ").Append(_typeName(host.Participant)).Append(".CreateDescriptor(")
            .Append(_typeName(network)).Append(".CreateOptions(), ")
            .Append(_typeName(network)).AppendLine(".Registry),")
            .Append("            global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.")
            .AppendLine(host.Binding == _serviceBusBinding ? "ServiceBus," : "StorageQueue,")
            .Append("            \"").Append(_escape(identity)).AppendLine("\",")
            .Append("            \"").Append(_escape(connection)).AppendLine("\",");

        _emitMaximumDeliveryCount(source, retryType, "            ", appendComma: true);
        source.AppendLine();
        source.Append(retryType is null
                ? "            global::System.TimeSpan.FromMinutes(5),"
                : "            new " + _typeName(retryType) + "().MaximumHandlerDuration,")
            .AppendLine();

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
        source.AppendLine(",");
        if (retryType is null)
            source.AppendLine("            global::System.TimeSpan.Zero,");
        else
            source.Append("            new ").Append(_typeName(retryType)).AppendLine("().RetryDelay,");
        source.Append("            ").Append(host.StrictStorageQueueHostSettings ? "true" : "false")
            .AppendLine(",")
            .AppendLine("            new global::Ark.Tools.MediatorFramework.Messaging.MessagingResourceManifest(")
            .Append("                \"").Append(_escape(identity)).AppendLine("\",")
            .Append("                ").Append(hasIdentityQueue ? "\"" + _escape(identity) + "\"" : "null")
            .AppendLine(",");
        _emitMaximumDeliveryCount(source, retryType, "                ", appendComma: true);
        source.AppendLine()
            .AppendLine("                new global::Ark.Tools.MediatorFramework.Messaging.MessagingTopicResource[]")
            .AppendLine("                {");
        foreach (var topic in desiredTopics.OrderBy(static topic => topic.Name, StringComparer.Ordinal))
        {
            source.AppendLine("                    new global::Ark.Tools.MediatorFramework.Messaging.MessagingTopicResource(")
                .Append("                        \"").Append(_escape(topic.Name)).AppendLine("\",")
                .Append("                        \"").Append(_escape(topic.OwnerIdentity)).AppendLine("\"),");
        }
        source.AppendLine("                },")
            .AppendLine("                new global::Ark.Tools.MediatorFramework.Messaging.MessagingSubscriptionResource[]")
            .AppendLine("                {");
        foreach (var subscription in subscriptions)
        {
            source.AppendLine("                    new global::Ark.Tools.MediatorFramework.Messaging.MessagingSubscriptionResource(")
                .Append("                        \"").Append(_escape(subscription.Topic)).AppendLine("\",")
                .Append("                        \"").Append(_escape(subscription.Name)).AppendLine("\",")
                .Append("                        \"").Append(_escape(subscription.ForwardToQueue)).AppendLine("\",");
            _emitMaximumDeliveryCount(source, retryType, "                        ", appendComma: true);
            source.AppendLine()
                .Append("                        \"").Append(_escape(identity)).AppendLine("\"),");
        }
        source.AppendLine("                },")
            .AppendLine("                new string[]")
            .AppendLine("                {");
        foreach (var topic in knownTopics.OrderBy(static topic => topic.Name, StringComparer.Ordinal))
            source.Append("                    \"").Append(_escape(topic.Name)).AppendLine("\",");
        source.AppendLine("                },")
            .Append("                (global::Ark.Tools.MediatorFramework.MessagingResourceLifecycle)")
            .Append(resourceLifecycle.ToString(CultureInfo.InvariantCulture)).AppendLine("));")
            .AppendLine();
    }

    private static void _emitMaximumDeliveryCount(
        StringBuilder source,
        INamedTypeSymbol? retryType,
        string indentation,
        bool appendComma)
    {
        source.Append(indentation);
        if (retryType is null)
        {
            source.Append('1');
        }
        else
        {
            source.Append("checked(new ").Append(_typeName(retryType))
                .Append("().MaximumDeliveryCount * (new ").Append(_typeName(retryType))
                .Append("().SecondLevelRetriesEnabled ? 2 : 1))");
        }
        if (appendComma)
            source.Append(',');
    }

    private static void _emitTypes(StringBuilder source, ImmutableArray<INamedTypeSymbol> types)
    {
        source.AppendLine("            new global::System.Type[]")
            .AppendLine("            {");
        foreach (var type in types)
            source.Append("                typeof(").Append(_typeName(type)).AppendLine("),");
        source.Append("            }");
    }

    private static void _emitTrigger(
        StringBuilder source,
        int binding,
        string identity,
        string connection)
    {
        var methodName = _functionName(identity);
        source.Append("    /// <summary>Receives the \"").Append(_escape(identity))
            .AppendLine("\" participant identity queue.</summary>")
            .Append("    [global::Microsoft.Azure.Functions.Worker.Function(\"")
            .Append(_escape(identity)).AppendLine("\")]")
            .Append("    public static async global::System.Threading.Tasks.Task ")
            .Append(methodName).AppendLine("(");
        if (binding == _serviceBusBinding)
        {
            source.AppendLine("        [global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(")
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
            return;
        }

        source.AppendLine("        [global::Microsoft.Azure.Functions.Worker.QueueTrigger(")
            .Append("            \"").Append(_escape(identity)).AppendLine("\",")
            .Append("            Connection = \"").Append(_escape(connection)).AppendLine("\")]")
            .AppendLine("        global::Azure.Storage.Queues.Models.QueueMessage message,")
            .AppendLine("        global::Microsoft.Azure.Functions.Worker.FunctionContext functionContext,")
            .AppendLine("        global::System.Threading.CancellationToken cancellationToken)")
            .AppendLine("    {")
            .AppendLine("        await global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingQueueFunctionsDispatcher")
            .Append("            .DispatchAsync(message, \"").Append(_escape(identity))
            .AppendLine("\", functionContext, cancellationToken)")
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

    private static void _validateHostJson(
        SourceProductionContext context,
        Host host,
        ImmutableArray<string?> hostJson)
    {
        var content = hostJson.FirstOrDefault(static value => value is not null);
        if (content is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_hostJsonNotInspectable, host.Location));
            return;
        }

        // ponytail: Build-time checks inspect three fixed literal properties; use a JSON parser
        // if the host contract grows beyond this flat Functions queues settings object.
        if (!_regexIsMatch(
                content,
                "\"messageEncoding\"\\s*:\\s*\"none\""))
        {
            context.ReportDiagnostic(Diagnostic.Create(_invalidMessageEncoding, host.Location));
        }
        if (!_regexIsMatch(
                content,
                "\"maxDequeueCount\"\\s*:\\s*[1-9][0-9]*"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _invalidMaximumDequeueCount,
                host.Location));
        }

        var visibility = Regex.Match(
            content,
            "\"visibilityTimeout\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (!visibility.Success
            || !TimeSpan.TryParse(
                visibility.Groups["value"].Value,
                CultureInfo.InvariantCulture,
                out var delay)
            || delay <= TimeSpan.Zero)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _invalidVisibilityTimeout,
                host.Location));
        }
    }

    private static bool _regexIsMatch(string value, string pattern)
    {
        return Regex.IsMatch(
            value,
            pattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
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
            bool strictStorageQueueHostSettings,
            Location location)
        {
            Participant = participant;
            Binding = binding;
            ConnectionConfigurationKey = connectionConfigurationKey;
            IncomingSteps = incomingSteps;
            OutgoingSteps = outgoingSteps;
            StrictStorageQueueHostSettings = strictStorageQueueHostSettings;
            Location = location;
        }

        public INamedTypeSymbol Participant { get; }

        public int Binding { get; }

        public string? ConnectionConfigurationKey { get; }

        public ImmutableArray<INamedTypeSymbol> IncomingSteps { get; }

        public ImmutableArray<INamedTypeSymbol> OutgoingSteps { get; }

        public bool StrictStorageQueueHostSettings { get; }

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

    private readonly struct Topic
    {
        public Topic(INamedTypeSymbol contract, string name, string ownerIdentity)
        {
            Contract = contract;
            Name = name;
            OwnerIdentity = ownerIdentity;
        }

        public INamedTypeSymbol Contract { get; }

        public string Name { get; }

        public string OwnerIdentity { get; }
    }
}
