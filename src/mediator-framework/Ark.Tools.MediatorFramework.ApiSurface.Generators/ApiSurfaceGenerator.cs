// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.ApiSurface;

/// <summary>Generates the deterministic transport API surface and emits per-contract diagnostics when the snapshot drifts.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ApiSurfaceGenerator : IIncrementalGenerator
{
    private const string Http = "Ark.Tools.MediatorFramework.HttpEndpointAttribute";
    private const string Grpc = "Ark.Tools.MediatorFramework.GrpcMethodAttribute";
    private const string GrpcService = "Ark.Tools.MediatorFramework.GrpcServiceAttribute";
    private const string Rebus = "Ark.Tools.MediatorFramework.RebusMessageAttribute";
    private const string Message = "Ark.Tools.MediatorFramework.MessageAttribute";
    private const string Event = "Ark.Tools.MediatorFramework.EventAttribute";
    private const string Participant = "Ark.Tools.MediatorFramework.MessagingParticipantAttribute";
    private const string Network = "Ark.Tools.MediatorFramework.MessagingNetworkAttribute";
    private const string MessagingHost = "Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHostAttribute";
    private const string GeneratedSurface = "Ark.Tools.MediatorFramework.MessagingGeneratedSurfaceAttribute";
    private const string ApiGroup = "Ark.Tools.MediatorFramework.ApiGroupAttribute";
    private const string ServerSet = "Ark.Tools.MediatorFramework.ServerSetAttribute";
    private const string Versioning = "Ark.Tools.MediatorFramework.VersioningAttribute";
    private const string McpTool = "Ark.Tools.MediatorFramework.McpToolAttribute";

    private static readonly DiagnosticDescriptor MissingSnapshot = new(
        "ARKAPI001",
        "API surface snapshot missing",
        "ArkApiSurface.txt is missing. Run 'dotnet build -p:EmitCompilerGeneratedFiles=true' to generate ArkApiSurface.current.txt, copy it to $(MSBuildProjectDirectory)/ArkApiSurface.txt, and commit it.",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ContractChanged = new(
        "ARKAPI002",
        "API surface contract changed",
        "Contract '{0}' has changed since the last accepted snapshot. Run 'dotnet build -p:EmitCompilerGeneratedFiles=true' to inspect ArkApiSurface.current.txt, then update ArkApiSurface.txt to accept this change.",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleSnapshots = new(
        "ARKAPI003",
        "Multiple API surface snapshots",
        "Only one ArkApiSurface.txt baseline is allowed, but {0} were found.",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MalformedSnapshot = new(
        "ARKAPI004",
        "Malformed API surface snapshot",
        "ArkApiSurface.txt contains an invalid snapshot line: '{0}'.",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var httpTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Http,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var grpcTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Grpc,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var rebusTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Rebus,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var messageTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Message,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var eventTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Event,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var participantTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Participant,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var networkTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Network,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var messagingHosts = context.SyntaxProvider.ForAttributeWithMetadataName(
                MessagingHost,
                static (_, _) => true,
                static (attributeContext, _) => ReadMessagingHosts(attributeContext))
            .Collect();
        var mcpTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                McpTool,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var contractTypes = httpTypes.Combine(grpcTypes).Combine(rebusTypes)
            .Combine(messageTypes).Combine(eventTypes).Combine(participantTypes).Combine(networkTypes).Combine(mcpTypes)
            .Select(static (pair, _) =>
            {
                var (((((((http, grpc), rebus), messages), events), participants), networks), mcp) = pair;
                return http.AddRange(grpc).AddRange(rebus)
                    .AddRange(messages).AddRange(events).AddRange(participants).AddRange(networks).AddRange(mcp);
            });
        var surfaceProvider = contractTypes.Combine(messagingHosts)
            .Select(static (input, cancellationToken) =>
                BuildSurface(
                    input.Left,
                    input.Right.SelectMany(static hosts => hosts).ToImmutableArray(),
                    cancellationToken));

        // Emit the .g.cs snapshot file (unchanged behaviour)
        context.RegisterSourceOutput(surfaceProvider, static (spc, surface) =>
        {
            var (lines, _) = surface;
            var text = string.Join("\n", lines) + (lines.Length == 0 ? string.Empty : "\n");
            spc.AddSource("ArkApiSurface.g.cs", "/*\n" + text.Replace("*/", "* /") + "*/\n");
        });

        // Read baseline from AdditionalFiles
        var baselineProvider = context.AdditionalTextsProvider
            .Where(static f => string.Equals(Path.GetFileName(f.Path), "ArkApiSurface.txt", StringComparison.OrdinalIgnoreCase))
            .Collect();

        // Read opt-out flag from MSBuild global properties
        var enabledProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (opts, _) =>
            {
                opts.GlobalOptions.TryGetValue("build_property.ArkApiSurfaceEnabled", out var v);
                // Only enable when the MSBuild property is explicitly set to "true"
                // (propagated via CompilerVisibleProperty in the buildTransitive .targets).
                // Absent means the project did not opt in or the .targets was not imported.
                return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            });

        // Emit per-contract diagnostics when baseline drifts
        context.RegisterSourceOutput(
            surfaceProvider.Combine(baselineProvider).Combine(enabledProvider),
            static (spc, combined) =>
            {
                var ((surface, baselineFiles), isEnabled) = combined;
                var (currentLines, locations) = surface;
                if (!isEnabled)
                    return;

                if (baselineFiles.Length > 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MultipleSnapshots, Location.None, baselineFiles.Length));
                    return;
                }

                if (baselineFiles.IsEmpty)
                {
                    // Only require a snapshot when there are actual contracts to track
                    if (currentLines.Length > 0)
                        spc.ReportDiagnostic(Diagnostic.Create(MissingSnapshot, Location.None));
                    return;
                }

                var baselineText = baselineFiles[0].GetText(spc.CancellationToken)?.ToString() ?? string.Empty;
                var parsedBaseline = ParseSnapshotLines(baselineText);
                if (!parsedBaseline.IsValid)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        MalformedSnapshot,
                        Location.None,
                        parsedBaseline.InvalidLine));
                    return;
                }

                var baselineSet = parsedBaseline.Lines;
                var currentSet = new HashSet<string>(currentLines, StringComparer.Ordinal);

                var changedOwners = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var line in currentSet.Except(baselineSet, StringComparer.Ordinal)
                    .Concat(baselineSet.Except(currentSet, StringComparer.Ordinal)))
                    changedOwners.Add(ContractOwner(line));

                foreach (var name in changedOwners)
                {
                    var loc = locations.TryGetValue(name, out var l) ? l : Location.None;
                    spc.ReportDiagnostic(Diagnostic.Create(ContractChanged, loc, name));
                }
            });
    }

    // Builds the sorted, deduplicated surface lines and a contract-name → Location index.
    private static (ImmutableArray<string> Lines, ImmutableDictionary<string, Location> Locations) BuildSurface(
        ImmutableArray<INamedTypeSymbol> contractTypes,
        ImmutableArray<MessagingHostInfo> messagingHosts,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var locBuilder = ImmutableDictionary.CreateBuilder<string, Location>(StringComparer.Ordinal);

        var types = contractTypes
            .GroupBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();
        var networkMemberships = BuildNetworkMemberships(types);

        foreach (var type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // ponytail: MinimallyQualifiedFormat == Name for non-generic non-nested types; generic
            // response types are interfaces/collections and never get a CONTRACT header, so mismatch
            // is not reachable in practice. Upgrade path: use FullyQualifiedFormat + strip namespace.
            var key = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (!locBuilder.ContainsKey(key))
                locBuilder[key] = type.Locations.FirstOrDefault() ?? Location.None;
            AddType(lines, type, networkMemberships);
        }
        foreach (var host in messagingHosts
            .OrderBy(static host => host.Participant.ToDisplayString(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddMessagingHostLines(lines, host);
        }

        var ordered = lines.Distinct(StringComparer.Ordinal)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToImmutableArray();
        return (ordered, locBuilder.ToImmutable());
    }

    private static SnapshotParseResult ParseSnapshotLines(string text)
    {
        var lines = text.TrimStart('\ufeff')
            .Split('\n')
            .Select(static l => l.TrimEnd('\r'))
            .Where(static l => l.Length > 0 && l != "/*" && l != "*/")
            .ToImmutableArray();
        var invalidLine = lines.FirstOrDefault(static line =>
            !line.StartsWith("CONTRACT ", StringComparison.Ordinal)
            && !line.StartsWith("REBUS ", StringComparison.Ordinal)
            && !line.StartsWith("ENUM ", StringComparison.Ordinal)
            && !line.StartsWith("EVOLVABLE-ENUM ", StringComparison.Ordinal)
            && !line.StartsWith("MESSAGE ", StringComparison.Ordinal)
            && !line.StartsWith("EVENT ", StringComparison.Ordinal)
            && !line.StartsWith("PARTICIPANT ", StringComparison.Ordinal)
            && !line.StartsWith("NETWORK ", StringComparison.Ordinal)
            && !line.StartsWith("MESSAGING-TRIGGER ", StringComparison.Ordinal)
            && !line.StartsWith("MESSAGING-ROUTE ", StringComparison.Ordinal));
        return invalidLine is null
            ? new SnapshotParseResult(new HashSet<string>(lines, StringComparer.Ordinal), true, string.Empty)
            : new SnapshotParseResult(new HashSet<string>(StringComparer.Ordinal), false, invalidLine);
    }

    // Extracts the contract owner name from a snapshot line.
    // "CONTRACT Foo -> ..."   → "Foo"
    // "CONTRACT Foo.Bar : T"  → "Foo"
    // "REBUS Foo -> queue:x"  → "Foo"
    private static string ContractOwner(string line)
    {
        var start = line.IndexOf(' ') + 1;
        if (start <= 0 || start >= line.Length)
            return line;
        var end = line.IndexOfAny(_ownerTerminators, start);
        return end < 0 ? line[start..] : line[start..end];
    }

    private static readonly char[] _ownerTerminators = { ' ', '.', '[' };

    private static Dictionary<INamedTypeSymbol, string[]> BuildNetworkMemberships(
        IEnumerable<INamedTypeSymbol> types)
    {
        var result = new Dictionary<INamedTypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        foreach (var network in types)
        {
            var attribute = Attribute(network, Network);
            if (attribute is null)
                continue;

            var networkName = network.ToDisplayString();
            foreach (var member in TypeSymbols(attribute, "Members"))
            {
                if (!result.TryGetValue(member, out var networks))
                    result.Add(member, networks = new List<string>());
                networks.Add(networkName);
            }
        }

        var memberships = new Dictionary<INamedTypeSymbol, string[]>(SymbolEqualityComparer.Default);
        foreach (var pair in result)
            memberships[pair.Key] = pair.Value
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        return memberships;
    }

    private static void AddType(
        List<string> lines,
        INamedTypeSymbol type,
        IReadOnlyDictionary<INamedTypeSymbol, string[]> networkMemberships)
    {
        var http = Attribute(type, Http);
        var grpc = Attribute(type, Grpc);
        var rebus = Attribute(type, Rebus);
        var message = Attribute(type, Message);
        var @event = Attribute(type, Event);
        var participant = Attribute(type, Participant);
        var network = Attribute(type, Network);
        if (http is null && grpc is null && rebus is null
            && message is null && @event is null && participant is null && network is null)
            return;

        var request = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        AddMessagingLines(lines, type, message, @event, participant, network, networkMemberships);
        if (message is not null || @event is not null || participant is not null || network is not null)
        {
            if (http is null && grpc is null && rebus is null)
                return;
        }
        var result = ResultType(type);
        var metadata = new List<string>();
        var group = StringArgument(Attribute(type, ApiGroup), 0) ?? "Ark";
        var versioning = Attribute(type, Versioning);
        var introduced = IntArgument(versioning, "Introduced", 1);
        var retired = IntArgument(versioning, "Retired", 0);
        var grpcGroup = StringArgument(Attribute(type, GrpcService), 0);
        if (grpcGroup is not null)
            metadata.Add($"grpc-group={grpcGroup}");
        if (http is not null)
        {
            metadata.Add($"http={StringArgument(http, 0)} {StringArgument(http, 1)}");
            metadata.Add($"version={introduced}{(retired == 0 ? "+" : $"-{retired - 1}")}");
        }

        if (grpc is not null)
        {
            metadata.Add($"grpc={StringArgument(grpc, 0) ?? type.Name}");
            metadata.Add($"grpc-version={introduced}{(retired == 0 ? "+" : $"-{retired - 1}")}");
        }

        lines.Add($"CONTRACT {request} -> {TypeName(result)} [group={group}]"
            + (metadata.Count == 0 ? string.Empty : " [" + string.Join("] [", metadata) + "]"));

        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in AllProperties(type))
            AddContract(lines, request, member, string.Empty, visited);
        if (result is INamedTypeSymbol resultType && resultType.TypeKind == TypeKind.Class
            && !SymbolEqualityComparer.Default.Equals(resultType, type))
        {
            lines.Add($"CONTRACT {resultType.Name}");
            foreach (var member in AllProperties(resultType))
                AddContract(lines, resultType.Name, member, string.Empty, visited);
        }
        else
        {
            // The result type is returned directly (not wrapped in a response class), e.g. an
            // enum or an EvolvableEnum<TEnum>: still emit its explicit member entries.
            AddEnumEntries(lines, result);
        }

        if (rebus is not null)
            lines.Add($"REBUS {request} -> queue:{StringNamed(rebus, "OwnerQueue") ?? "default"}");
    }

    private static void AddMessagingLines(
        List<string> lines,
        INamedTypeSymbol type,
        AttributeData? message,
        AttributeData? @event,
        AttributeData? participant,
        AttributeData? network,
        IReadOnlyDictionary<INamedTypeSymbol, string[]> networkMemberships)
    {
        var clrName = type.ToDisplayString();
        if (message is not null)
            lines.Add($"MESSAGE {clrName} -> name:{ContractName(type, message)} former:{FormatSet(StringsNamed(message, "FormerNames"))}");
        if (@event is not null)
            lines.Add($"EVENT {clrName} -> name:{ContractName(type, @event)} former:{FormatSet(StringsNamed(@event, "FormerNames"))}");
        if (participant is not null)
        {
            var networkName = networkMemberships.TryGetValue(type, out var networks)
                ? string.Join("|", networks)
                : "-";
            var identity = StringNamed(participant, "Identity") ?? NormalizeIdentity(
                type.Name.EndsWith("Participant", StringComparison.Ordinal)
                    ? type.Name[..^"Participant".Length]
                    : type.Name);
            lines.Add($"PARTICIPANT {clrName} -> network:{networkName} identity:{identity}"
                + $" processes:{FormatSet(ContractNames(participant, "Processes"))}"
                + $" publishes:{FormatSet(ContractNames(participant, "Publishes"))}"
                + $" subscribes:{FormatSet(ContractNames(participant, "Subscribes"))}"
                + $" serializers:{FormatSet(EnumNames(participant, "Serializers"))}"
                + $" default:{EnumName(participant, "DefaultSerializer")}");
        }
        if (network is not null)
        {
            lines.Add($"NETWORK {clrName} -> members:{FormatSet(TypeNames(network, "Members"))}"
                + $" requires:{FormatFlags(EnumValue(network, "Requires"))}");
        }
    }

    private static ImmutableArray<MessagingHostInfo> ReadMessagingHosts(
        GeneratorAttributeSyntaxContext context)
        {
            var hosts = ImmutableArray.CreateBuilder<MessagingHostInfo>();
            foreach (var attribute in context.Attributes)
            {
                if (attribute.ConstructorArguments.Length < 2
                    || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol participant
                    || attribute.ConstructorArguments[1].Value is not int binding)
                    continue;
                var syntax = attribute.ApplicationSyntaxReference;
                hosts.Add(new MessagingHostInfo(
                    participant,
                    binding,
                    syntax is null ? Location.None : Location.Create(syntax.SyntaxTree, syntax.Span)));
            }
            return hosts.ToImmutable();
        }

        private static void AddMessagingHostLines(List<string> lines, MessagingHostInfo host)
        {
            var participant = Attribute(host.Participant, Participant);
            if (participant is null)
                return;

            var identity = StringNamed(participant, "Identity") ?? NormalizeIdentity(
                host.Participant.Name.EndsWith("Participant", StringComparison.Ordinal)
                    ? host.Participant.Name[..^"Participant".Length]
                    : host.Participant.Name);
            var network = AllTypes(host.Participant.ContainingAssembly.GlobalNamespace)
                .Select(type => (Type: type, Attribute: Attribute(type, Network)))
                .FirstOrDefault(item => item.Attribute is not null
                    && TypeSymbols(item.Attribute, "Members").Any(member =>
                        SymbolEqualityComparer.Default.Equals(member, host.Participant)));
            var networkName = network.Type?.ToDisplayString() ?? "-";
            var binding = host.Binding switch
            {
                0 => "service_bus",
                1 => "storage_queue",
                _ => host.Binding.ToString(CultureInfo.InvariantCulture),
            };
            if (TypeSymbols(participant, "Processes").Any()
                || TypeSymbols(participant, "Subscribes").Any())
            {
                lines.Add($"MESSAGING-TRIGGER {host.Participant.ToDisplayString()}"
                    + $" -> binding:{binding} queue:{identity} network:{networkName}");
            }

            if (network.Attribute is null)
                return;
            var members = TypeSymbols(network.Attribute, "Members")
                .Select(member => (Type: member, Attribute: Attribute(member, Participant)))
                .Where(static item => item.Attribute is not null)
                .ToArray();
            foreach (var subscribedEvent in TypeSymbols(participant, "Subscribes")
                .OrderBy(static type => type.ToDisplayString(), StringComparer.Ordinal))
            {
                var publisher = members.SingleOrDefault(item =>
                    TypeSymbols(item.Attribute!, "Publishes").Any(contract =>
                        SymbolEqualityComparer.Default.Equals(contract, subscribedEvent)));
                if (publisher.Attribute is null)
                    continue;
                var publisherIdentity = StringNamed(publisher.Attribute, "Identity") ?? NormalizeIdentity(
                    publisher.Type.Name.EndsWith("Participant", StringComparison.Ordinal)
                        ? publisher.Type.Name[..^"Participant".Length]
                        : publisher.Type.Name);
                var logicalName = ContractName(
                    subscribedEvent,
                    Attribute(subscribedEvent, Message) ?? Attribute(subscribedEvent, Event));
                var topic = publisherIdentity + "-" + logicalName;
                var prefix = identity[..Math.Min(identity.Length, 41)];
                var subscription = prefix + "-" + StableHash(topic);
                lines.Add($"MESSAGING-ROUTE {host.Participant.ToDisplayString()}"
                    + $" -> event:{logicalName} topic:{topic} subscription:{subscription} forward:{identity}");
            }
        }

        private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
            {
                yield return type;
                foreach (var nested in NestedTypes(type))
                    yield return nested;
            }
            foreach (var child in @namespace.GetNamespaceMembers())
            {
                foreach (var type in AllTypes(child))
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> NestedTypes(INamedTypeSymbol type)
        {
            foreach (var nested in type.GetTypeMembers())
            {
                yield return nested;
                foreach (var descendant in NestedTypes(nested))
                    yield return descendant;
            }
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value)
                    hash = (hash ^ character) * 16777619u;
                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    private static string ContractName(INamedTypeSymbol type, AttributeData? attribute)
        => attribute is null
            ? NormalizeSnake(type.ToDisplayString())
            : StringNamed(attribute, "Name") ?? NormalizeSnake(type.ToDisplayString());

    private static string[] ContractNames(AttributeData attribute, string name)
        => TypeSymbols(attribute, name).Select(symbol => ContractName(
                symbol,
                Attribute(symbol, Message) ?? Attribute(symbol, Event)))
            .ToArray();

    private static string[] TypeNames(AttributeData attribute, string name)
        => TypeSymbols(attribute, name).Select(static symbol => symbol.ToDisplayString()).ToArray();

    private static string[] EnumNames(AttributeData attribute, string name)
        => NamedArray(attribute, name).Select(value => value.Value is int number
            ? ProtocolName(number)
            : string.Empty).Where(static value => value.Length > 0).ToArray();

    private static string EnumName(AttributeData attribute, string name)
    {
        var value = EnumValue(attribute, name);
        return ProtocolName(value);
    }

    private static string ProtocolName(int value) => value switch
    {
        0 => "json",
        1 => "messagepack",
        2 => "protobuf",
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    private static string FormatFlags(int flags)
    {
        var values = new[] { (1, "receive"), (2, "pubsub"), (4, "scheduled_send") };
        var result = values.Where(value => (flags & value.Item1) != 0).Select(value => value.Item2).ToArray();
        return result.Length == 0 ? "-" : string.Join("|", result);
    }

    private static string FormatSet(IEnumerable<string> values)
    {
        var sorted = values.Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        return sorted.Length == 0 ? "-" : string.Join("|", sorted);
    }

    private static string[] StringsNamed(AttributeData attribute, string name)
        => NamedArray(attribute, name).Where(value => value.Value is string)
            .Select(value => (string)value.Value!).ToArray();

    private static ImmutableArray<TypedConstant> NamedArray(AttributeData attribute, string name)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value;
        return argument.Kind == TypedConstantKind.Array ? argument.Values : ImmutableArray<TypedConstant>.Empty;
    }

    private static int EnumValue(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value;
        return value is int number ? number : 0;
    }

    private static IEnumerable<INamedTypeSymbol> TypeSymbols(AttributeData attribute, string name)
        => NamedArray(attribute, name).Where(value => value.Value is INamedTypeSymbol)
            .Select(value => (INamedTypeSymbol)value.Value!);

    private static string NormalizeIdentity(string value)
        => string.Join("-", Words(value).Select(static word => word.ToLowerInvariant()));

    private static string NormalizeSnake(string value)
        => string.Join("_", value.Split('.').SelectMany(Words).Select(static word => word.ToLowerInvariant()));

    private static IEnumerable<string> Words(string value)
    {
        var word = new System.Text.StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var startsWord = index > 0 && char.IsUpper(character)
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

    private static void AddContract(List<string> lines, string owner, IPropertySymbol property, string prefix, HashSet<ITypeSymbol> visited)
    {
        if (property.Name == "EqualityContract")
            return;
        var path = prefix + property.Name;
        var serverSet = Attribute(property, ServerSet) is not null;
        var type = Unwrap(property.Type, out var collection);
        if (type is INamedTypeSymbol named && named.TypeKind == TypeKind.Class && named.SpecialType == SpecialType.None
            && named.ContainingAssembly.Name == property.ContainingAssembly.Name
            && visited.Add(named))
        {
            foreach (var child in AllProperties(named))
                AddContract(lines, owner, child, path + (collection ? "[]." : "."), visited);
            visited.Remove(named);
        }
        else
        {
            lines.Add($"CONTRACT {owner}.{path}{(collection ? "[]" : string.Empty)} : {TypeName(property.Type)}"
                + (serverSet ? " server-set=true" : string.Empty)
                + DefaultValue(property));
            AddEnumEntries(lines, type);
        }
    }

    private static ITypeSymbol ResultType(INamedTypeSymbol type)
    {
        // Look for IQuery<TResult> or IRequest<TResponse> (the 1-arg base interface that carries the
        // result type). When the type uses the self-generic 2-arg variant such as IQuery<TSelf, TResult>,
        // Roslyn's AllInterfaces also surfaces the 1-arg base, so filtering to TypeArguments.Length == 1
        // correctly resolves the result type for both the legacy and self-generic patterns.
        var resultIface = type.AllInterfaces.FirstOrDefault(
            x => (x.Name is "IQuery" or "IRequest") && x.TypeArguments.Length == 1);
        if (resultIface is not null)
            return resultIface.TypeArguments[0];

        // ICommand / ICommand<TSelf> have no result type; the contract type itself is its own identity.
        return type;
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type, out bool collection)
    {
        collection = type is IArrayTypeSymbol;
        if (type is IArrayTypeSymbol array)
            return array.ElementType;
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1
            && named.AllInterfaces.Any(x => x.OriginalDefinition.SpecialType == SpecialType.None && x.Name is "IEnumerable" or "IReadOnlyCollection" or "IReadOnlyList" or "List"))
        {
            collection = true;
            return named.TypeArguments[0];
        }
        return type;
    }

    // Emits explicit member/value entries for enum types reached from contract members, either
    // used directly ("strict enum") or wrapped in Ark.Tools.Core.EvolvableEnum<TEnum> ("evolvable
    // enum"), so that adding/removing/renumbering members is caught as an API surface drift.
    private static void AddEnumEntries(List<string> lines, ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return;

        if (named.TypeKind == TypeKind.Enum)
        {
            AddEnumMembers(lines, "ENUM", named);
        }
        else if (TryUnwrapEvolvableEnum(named, out var enumType))
        {
            AddEnumMembers(lines, "EVOLVABLE-ENUM", enumType);
        }
    }

    // Detects Ark.Tools.Core.EvolvableEnum<TEnum> by name/arity/namespace (no compile-time
    // reference to Ark.Tools.Core is required, matching the existing attribute-name-matching
    // convention used throughout this generator).
    private static bool TryUnwrapEvolvableEnum(INamedTypeSymbol named, out INamedTypeSymbol enumType)
    {
        if (named.IsGenericType && named.Arity is 1 or 2
            && named.OriginalDefinition.Name == "EvolvableEnum"
            && named.ContainingNamespace?.ToDisplayString() == "Ark.Tools.Core"
            && named.TypeArguments[0] is INamedTypeSymbol argument && argument.TypeKind == TypeKind.Enum)
        {
            enumType = argument;
            return true;
        }

        enumType = null!;
        return false;
    }

    private static void AddEnumMembers(List<string> lines, string kind, INamedTypeSymbol enumType)
    {
        var name = TypeName(enumType);
        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasConstantValue))
            lines.Add($"{kind} {name}.{field.Name}={Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture)}");
    }

    private static IEnumerable<IPropertySymbol> AllProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                if (!IsGeneratedSurface(property))
                    yield return property;
    }

    private static bool IsGeneratedSurface(ISymbol symbol) =>
        symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == GeneratedSurface);

    private static AttributeData? Attribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == name);

    private static string? StringArgument(AttributeData? attribute, int index) =>
        attribute is not null && attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;

    private static string? StringNamed(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as string;

    private static bool BoolNamed(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as bool? == true;

    private static int IntArgument(AttributeData? attribute, string? name, int fallback)
    {
        if (attribute is null)
            return fallback;
        if (name is not null)
            return attribute.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as int? ?? fallback;
        return attribute.ConstructorArguments.Length == 0 ? fallback : attribute.ConstructorArguments[0].Value as int? ?? fallback;
    }

    private static string TypeName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Replace(" ", string.Empty);

    private static string DefaultValue(IPropertySymbol property)
    {
        var syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is not Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax declaration
            || declaration.Initializer is null)
            return string.Empty;

        var value = declaration.Initializer.Value.NormalizeWhitespace().ToFullString()
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("*/", "* /");
        return $" default={value}";
    }

    private readonly record struct SnapshotParseResult(
        HashSet<string> Lines,
        bool IsValid,
        string InvalidLine);

    private readonly record struct MessagingHostInfo(
        INamedTypeSymbol Participant,
        int Binding,
        Location Location);
}
