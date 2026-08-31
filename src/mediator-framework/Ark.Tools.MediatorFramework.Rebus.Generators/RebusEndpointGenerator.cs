// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Ark.Tools.MediatorFramework.Generators;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.MediatorFramework.Generators
{
    /// <summary>
    /// Incremental generator that emits legacy <c>[RebusMessage]</c> endpoints and participant-bound
    /// Rebus host helpers. Participant handlers are nested in the sealed partial class marked with
    /// <c>ArkRebusHostAttribute</c>. Only the Rebus transport is emitted by this generator; add
    /// <c>Ark.Tools.MediatorFramework.MinimalApi.Generators</c> for HTTP and
    /// <c>Ark.Tools.MediatorFramework.Grpc.Generators</c> for gRPC.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkRebusEndpointGenerator : IIncrementalGenerator
    {
        private const string RebusMessageAttribute = "Ark.Tools.MediatorFramework.RebusMessageAttribute";
        private const string ArkGenerateRebusForAssemblyAttribute = "Ark.Tools.MediatorFramework.Rebus.ArkGenerateRebusForAssemblyAttribute";
        private const string ArkRebusHostAttribute = "Ark.Tools.MediatorFramework.Rebus.ArkRebusHostAttribute";
        private const string MessagingNetworkAttribute = "Ark.Tools.MediatorFramework.MessagingNetworkAttribute";
        private const string MessagingParticipantAttribute = "Ark.Tools.MediatorFramework.MessagingParticipantAttribute";
        private static readonly DiagnosticDescriptor InvalidOwnerQueue = new(
            "ARKMF004", "Invalid Rebus owner queue",
            "The Rebus owner queue for '{0}' must not be blank", "Rebus",
            DiagnosticSeverity.Error, isEnabledByDefault: true);
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpointMappings = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is InvocationExpressionSyntax,
                    static (syntaxContext, cancellationToken) =>
                        GetAssemblyMapping(syntaxContext, cancellationToken))
                .Where(static mapping => mapping is not null)
                .Select(static (mapping, _) => mapping!.Value);
            var endpointAssemblies = endpointMappings
                .SelectMany(static (mapping, _) => mapping.AssemblyNames)
                .Collect();
            var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                    RebusMessageAttribute,
                    static (_, _) => true,
                    static (attributeContext, _) => ExtractSourceEndpoint(attributeContext))
                .Where(static endpoint => endpoint is not null)
                .Select(static (endpoint, _) => endpoint!.Value);
            var referencedEndpoints = context.CompilationProvider
                .Combine(endpointAssemblies)
                .SelectMany(static (pair, cancellationToken) =>
                    GetReferencedEndpoints(pair.Left, pair.Right, cancellationToken));

            var hosts = context.CompilationProvider.Select(
                static (compilation, cancellationToken) => ReadHosts(compilation, cancellationToken));
            var collected = sourceEndpoints.Collect().Combine(referencedEndpoints.Collect()).Combine(hosts);

            context.RegisterSourceOutput(
                collected,
                static (spc, item) => Emit(spc, item.Left.Left.AddRange(item.Left.Right), item.Right));
        }

        private static EndpointModel? ExtractSourceEndpoint(GeneratorAttributeSyntaxContext context)
        {
            return Extract((INamedTypeSymbol)context.TargetSymbol, context.Attributes[0]);
        }

        private static AssemblyMapping? GetAssemblyMapping(
            GeneratorSyntaxContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = (InvocationExpressionSyntax)context.Node;
            var method = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var genericName = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .FirstOrDefault(name => name.Identifier.ValueText is "RegisterArkRebusHandlersFromAssembly"
                    or "RegisterArkRebusHandlers"
                    or "ConfigureArkRebusRouting");
            if (genericName is null || genericName.TypeArgumentList.Arguments.Count != 1)
                return null;
            if (method is not null
                ? !string.Equals(method.MetadataName, genericName.Identifier.ValueText, StringComparison.Ordinal)
                : !IsGeneratedEndpointInvocation(invocation, genericName.Identifier.ValueText))
                return null;

            var contextAssemblyNames = GetContextAssemblyNames(context, genericName, cancellationToken);
            var assemblyNames = genericName.Identifier.ValueText is "RegisterArkRebusHandlers"
                || genericName.Identifier.ValueText == "ConfigureArkRebusRouting" && !contextAssemblyNames.IsDefaultOrEmpty
                ? contextAssemblyNames
                : GetAssemblyMarkerName(context, genericName, cancellationToken);
            return assemblyNames.IsDefaultOrEmpty ? null : new AssemblyMapping(assemblyNames);
        }

        private static ImmutableArray<string> GetAssemblyMarkerName(
            GeneratorSyntaxContext context,
            GenericNameSyntax genericName,
            CancellationToken cancellationToken)
        {
            return context.SemanticModel
                .GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken)
                .Type?.ContainingAssembly?.Name is { } assemblyName
                ? ImmutableArray.Create(assemblyName)
                : ImmutableArray<string>.Empty;
        }

        private static ImmutableArray<string> GetContextAssemblyNames(
            GeneratorSyntaxContext context,
            GenericNameSyntax genericName,
            CancellationToken cancellationToken)
        {
            if (context.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken).Type
                is not INamedTypeSymbol contextType)
                return ImmutableArray<string>.Empty;

            return contextType.GetAttributes()
                .Where(attribute => attribute.AttributeClass?.ToDisplayString() == ArkGenerateRebusForAssemblyAttribute)
                .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
                .Where(static marker => marker?.ContainingAssembly?.Name is not null)
                .Select(static marker => marker!.ContainingAssembly!.Name)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static bool IsGeneratedEndpointInvocation(InvocationExpressionSyntax invocation, string methodName)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name is not GenericNameSyntax genericName
                || !string.Equals(genericName.Identifier.ValueText, methodName, StringComparison.Ordinal))
                return false;

            return memberAccess.Expression.DescendantNodesAndSelf()
                .OfType<SimpleNameSyntax>()
                .Any(name => string.Equals(name.Identifier.ValueText, "ArkGeneratedEndpoints", StringComparison.Ordinal));
        }

        private static ImmutableArray<EndpointModel> GetReferencedEndpoints(
            Compilation compilation,
            ImmutableArray<string> endpointAssemblies,
            CancellationToken cancellationToken)
        {
            var rebusAttr = compilation.GetTypeByMetadataName(RebusMessageAttribute);
            if (rebusAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = rebusAttr.ContainingAssembly;
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();
            var requestedAssemblies = endpointAssemblies.ToHashSet(StringComparer.Ordinal);

            foreach (var assembly in _referencedAssemblies(compilation, runtimeAssembly)
                .Where(assembly => requestedAssemblies.Contains(assembly.Name)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attrs = type.GetAttributes();
                    var rebus = attrs.FirstOrDefault(
                        a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, rebusAttr));
                    if (rebus is null)
                        continue;

                    var model = Extract(type, rebus);
                    if (model is not null)
                        builder.Add(model.Value);
                }
            }

            return builder.ToImmutable();
        }

        private static bool IsType(INamedTypeSymbol type, string name, string @namespace)
            => type.MetadataName == name
                && type.ContainingNamespace.ToDisplayString() == @namespace;

        private static IEnumerable<IAssemblySymbol> _referencedAssemblies(Compilation compilation, IAssemblySymbol runtimeAssembly)
        {
            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols
                .Where(reference => !SymbolEqualityComparer.Default.Equals(reference, runtimeAssembly)))
            {
                var referencesRuntime = reference.Modules.Any(
                    m => m.ReferencedAssemblies.Any(
                        id => string.Equals(id.Name, runtimeAssembly.Name, StringComparison.Ordinal)));

                if (referencesRuntime)
                    yield return reference;
            }
        }

        private static IEnumerable<INamedTypeSymbol> _allTypes(INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    foreach (var type in _allTypes(childNs))
                        yield return type;
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    foreach (var nested in AllNestedTypes(type))
                        yield return nested;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> AllNestedTypes(INamedTypeSymbol type)
        {
            foreach (var nested in type.GetTypeMembers())
            {
                yield return nested;
                foreach (var child in AllNestedTypes(nested))
                    yield return child;
            }
        }

        private static EndpointModel? Extract(INamedTypeSymbol type, AttributeData rebusAttribute)
        {
            // Rebus messages are dispatched via their matching Solid handler; queries (reads) are not
            // meaningful as bus messages.
            foreach (var iface in type.AllInterfaces)
            {
                if (IsType(iface.OriginalDefinition, "ICommand", "Ark.Tools.Solid"))
                {
                    var diagnostics = new List<DiagnosticInfo>();
                    var ownerQueue = GetOwnerQueue(rebusAttribute);
                    if (ownerQueue is null && HasOwnerQueueArgument(rebusAttribute))
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            InvalidOwnerQueue,
                            GeneratedName(type),
                            GetLocation(rebusAttribute)));
                    }

                    return new EndpointModel(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        GeneratedName(type),
                        null,
                        ownerQueue is not null && diagnostics.Count == 0 ? ownerQueue : null,
                        diagnostics,
                        isCommand: true,
                        location: GetLocation(rebusAttribute));
                }

                if (IsType(iface.OriginalDefinition, "IRequest`1", "Ark.Tools.Solid"))
                {
                    var response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var diagnostics = new List<DiagnosticInfo>();
                    if (IsAsyncEnumerable(iface.TypeArguments[0]))
                        diagnostics.Add(new DiagnosticInfo(
                            DiagnosticDescriptors.StreamingResponseNotSupported,
                            type.Name,
                            GetLocation(rebusAttribute)));
                    var ownerQueue = GetOwnerQueue(rebusAttribute);
                    if (ownerQueue is null && HasOwnerQueueArgument(rebusAttribute))
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            InvalidOwnerQueue,
                            type.Name,
                            GetLocation(rebusAttribute)));
                    }

                    return new EndpointModel(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        GeneratedName(type),
                        response,
                        ownerQueue is not null && diagnostics.Count == 0 ? ownerQueue : null,
                        diagnostics,
                        isCommand: false,
                        location: GetLocation(rebusAttribute));
                }

            }

            return EndpointModel.Invalid(
                type,
                new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedHandlerKind,
                    type.Name,
                    GetLocation(rebusAttribute)));
        }

        private static string? GetOwnerQueue(AttributeData attribute)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "OwnerQueue");
            var ownerQueue = argument.Value.Value as string;
            return string.IsNullOrWhiteSpace(ownerQueue) ? null : ownerQueue;
        }

        private static bool HasOwnerQueueArgument(AttributeData attribute)
            => attribute.NamedArguments.Any(pair => pair.Key == "OwnerQueue");

        private static bool IsAsyncEnumerable(ITypeSymbol type)
            => type is INamedTypeSymbol named
                && (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IAsyncEnumerable<T>"
                    || named.AllInterfaces.Any(iface => iface.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IAsyncEnumerable<T>"));

        private static Location GetLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        private static bool MatchesAttribute(AttributeData attribute, string @namespace, string name)
        {
            var attributeClass = attribute.AttributeClass;
            return attributeClass is not null
                && string.Equals(attributeClass.ContainingNamespace.ToDisplayString(), @namespace, StringComparison.Ordinal)
                && string.Equals(attributeClass.Name, name, StringComparison.Ordinal);
        }

        private static INamedTypeSymbol? GetParticipantType(AttributeData attribute)
        {
            if (attribute.AttributeClass is { TypeArguments: { Length: > 0 } }
                && attribute.AttributeClass.TypeArguments[0] is INamedTypeSymbol participant)
                return participant;

            return attribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
        }

        private static ImmutableArray<HostModel> ReadHosts(
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            return _allTypes(compilation.Assembly.GlobalNamespace)
                .Where(type => type.GetAttributes().Any(attribute =>
                    MatchesAttribute(attribute, "Ark.Tools.MediatorFramework.Rebus", "ArkRebusHostAttribute")))
                .Select(type => ReadHost(compilation, type, cancellationToken))
                .ToImmutableArray();
        }

        private static HostModel ReadHost(
            Compilation compilation,
            INamedTypeSymbol hostType,
            CancellationToken cancellationToken)
        {
            var hostAttribute = hostType.GetAttributes().FirstOrDefault(
                attribute => MatchesAttribute(attribute, "Ark.Tools.MediatorFramework.Rebus", "ArkRebusHostAttribute"));
            var participant = hostAttribute is null ? null : GetParticipantType(hostAttribute);
            if (participant is null)
                return HostModel.Invalid(
                    "ArkRebusHostAttribute must reference a messaging participant.",
                    hostAttribute is null ? Location.None : GetLocation(hostAttribute));

            cancellationToken.ThrowIfCancellationRequested();
            if (!hostType.IsSealed
                || hostType.IsStatic
                || hostType.ContainingType is not null
                || hostType.Arity != 0
                || !hostType.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration
                    && declaration.Modifiers.Any(modifier => modifier.ValueText == "partial")))
            {
                return HostModel.Invalid(
                    "ArkRebusHostAttribute must target a top-level, non-generic sealed partial class.",
                    GetLocation(hostAttribute!));
            }
            var participantAttribute = participant.GetAttributes().FirstOrDefault(
                attribute => MatchesAttribute(attribute, "Ark.Tools.MediatorFramework", "MessagingParticipantAttribute"));
            if (participantAttribute is null)
                return HostModel.Invalid("The Rebus host binding must reference a messaging participant.", GetLocation(hostAttribute!));

            var networks = _assemblies(compilation)
                .SelectMany(assembly => _allTypes(assembly.GlobalNamespace))
                .Select(type => (Type: type, Attribute: type.GetAttributes().FirstOrDefault(
                    attribute => MatchesAttribute(attribute, "Ark.Tools.MediatorFramework", "MessagingNetworkAttribute"))))
                .Where(item => item.Attribute is not null && _types(item.Attribute!, "Members")
                    .Any(member => SymbolEqualityComparer.Default.Equals(member, participant)))
                .ToArray();
            if (networks.Length != 1)
            {
                return HostModel.Invalid(
                    networks.Length == 0
                        ? "The bound messaging participant is not listed in a messaging network."
                        : "The bound messaging participant is listed in more than one messaging network.",
                    GetLocation(hostAttribute!));
            }

            var identity = _string(participantAttribute, "Identity")
                ?? NormalizeIdentity(participant.Name.EndsWith("Participant", StringComparison.Ordinal)
                    ? participant.Name.Substring(0, participant.Name.Length - "Participant".Length)
                    : participant.Name);
            var processes = _types(participantAttribute, "Processes");
            var publishes = _types(participantAttribute, "Publishes");
            var subscribes = _types(participantAttribute, "Subscribes");
            var retryType = _type(participantAttribute, "Retry");
            var compression = _enum(participantAttribute, "Compression");
            var networkAttribute = networks[0].Attribute!;
            var routes = ImmutableArray.CreateBuilder<EndpointModel>();
            foreach (var member in _types(networkAttribute, "Members"))
            {
                var declaration = member.GetAttributes().FirstOrDefault(
                    attribute => MatchesAttribute(attribute, "Ark.Tools.MediatorFramework", "MessagingParticipantAttribute"));
                if (declaration is null)
                    continue;
                var owner = _string(declaration, "Identity")
                    ?? NormalizeIdentity(member.Name.EndsWith("Participant", StringComparison.Ordinal)
                        ? member.Name.Substring(0, member.Name.Length - "Participant".Length)
                        : member.Name);
                foreach (var contract in _types(declaration, "Processes"))
                {
                    var endpoint = ExtractParticipantContract(
                        contract,
                        owner,
                        member,
                        _enum(declaration, "DefaultSerializer"),
                        GetLocation(declaration));
                    if (endpoint is not null)
                        routes.Add(endpoint.Value);
                }
            }
            var adapters = processes.Concat(subscribes)
                .Select(contract => ExtractParticipantContract(
                    contract,
                    null,
                    participant,
                    _enum(participantAttribute, "DefaultSerializer"),
                    GetLocation(participantAttribute)))
                .Where(static endpoint => endpoint is not null)
                .Select(static endpoint => endpoint!.Value)
                .ToImmutableArray();
            var legacyEndpoints = adapters.IsDefaultOrEmpty
                ? ImmutableArray<EndpointModel>.Empty
                : _allTypes(participant.ContainingAssembly.GlobalNamespace)
                    .Select(type => (Type: type, Attribute: type.GetAttributes().FirstOrDefault(
                        attribute => attribute.AttributeClass?.ToDisplayString() == RebusMessageAttribute)))
                    .Where(static item => item.Attribute is not null)
                    .Select(static item => Extract(item.Type, item.Attribute!))
                    .Where(static endpoint => endpoint is not null)
                    .Select(static endpoint => endpoint!.Value)
                    .ToImmutableArray();

            return new HostModel(
                hostType.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : hostType.ContainingNamespace.ToDisplayString(),
                hostType.Name,
                hostType.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
                participant.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                identity,
                processes.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray(),
                publishes.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray(),
                subscribes.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray(),
                retryType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                compression != 0,
                false,
                routes.ToImmutable(),
                adapters,
                legacyEndpoints,
                null,
                GetLocation(hostAttribute!));
        }

        private static EndpointModel? ExtractParticipantContract(
            INamedTypeSymbol type,
            string? ownerQueue,
            INamedTypeSymbol owner,
            int protocol,
            Location location)
        {
            var diagnostics = new List<DiagnosticInfo>();
            MessagingContractTopologyValidator._validate(
                (descriptor, diagnosticLocation, arguments) =>
                    diagnostics.Add(new DiagnosticInfo(
                        descriptor,
                        type.Name,
                        diagnosticLocation,
                        arguments)),
                type,
                owner,
                protocol);
            foreach (var iface in type.AllInterfaces)
            {
                if (IsType(iface.OriginalDefinition, "ICommand", "Ark.Tools.Solid"))
                {
                    return new EndpointModel(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        GeneratedName(type),
                        null,
                        ownerQueue,
                        diagnostics,
                        isCommand: true,
                        location);
                }
                if (IsType(iface.OriginalDefinition, "IRequest`1", "Ark.Tools.Solid"))
                {
                    return new EndpointModel(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        GeneratedName(type),
                        iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        ownerQueue,
                        diagnostics,
                        isCommand: false,
                        location);
                }
            }
            return null;
        }

        private static IEnumerable<IAssemblySymbol> _assemblies(Compilation compilation)
        {
            yield return compilation.Assembly;
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                yield return assembly;
        }

        private static ImmutableArray<INamedTypeSymbol> _types(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value;
            if (argument.Kind != TypedConstantKind.Array)
                return ImmutableArray<INamedTypeSymbol>.Empty;
            return argument.Values
                .Select(value => value.Value)
                .OfType<INamedTypeSymbol>()
                .ToImmutableArray();
        }

        private static INamedTypeSymbol? _type(AttributeData attribute, string name)
            => attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as INamedTypeSymbol;

        private static string? _string(AttributeData attribute, string name)
            => attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;

        private static int _int(AttributeData attribute, string name)
            => attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value is int value ? value : 0;

        private static int _enum(AttributeData attribute, string name)
            => attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value is int value ? value : 0;

        private static string NormalizeIdentity(string value)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (index > 0 && char.IsUpper(character))
                    builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static void Emit(
            SourceProductionContext spc,
            ImmutableArray<EndpointModel> items,
            ImmutableArray<HostModel> hosts)
        {
            if (items.IsDefaultOrEmpty && hosts.IsDefaultOrEmpty)
                return;

            foreach (var invalidHost in hosts.Where(static host => host.Error is not null))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ARKMF020",
                        "Invalid Rebus participant host binding",
                        "{0}",
                        "Ark.Tools.MediatorFramework",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    invalidHost.Location,
                    invalidHost.Error));
            }
            hosts = hosts.Where(static host => host.Error is null).ToImmutableArray();

            var legacyRegistrationItems = items;
            var validationItems = items.AddRange(hosts.SelectMany(static host =>
                host.Routes.AddRange(host.Adapters).AddRange(host.LegacyEndpoints)));
            validationItems = validationItems.OrderBy(static item => item.TypeFullName, StringComparer.Ordinal).ToImmutableArray();
            foreach (var item in validationItems)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                foreach (var diagnostic in item.Diagnostics)
                    spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
            }
            foreach (var group in validationItems.Where(static item => item.IsValid).GroupBy(static item => item.TypeFullName))
            {
                var validItems = group.ToArray();
                if (validItems.Length < 2)
                    continue;

                var queues = validItems
                    .Select(static item => item.OwnerQueue)
                    .Where(static queue => queue is not null)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (queues.Length <= 1)
                    continue;
                foreach (var item in validItems)
                {
                    var diagnostic = new DiagnosticInfo(
                        DiagnosticDescriptors.ConflictingOwnerQueue,
                        item.TypeName,
                        item.Location,
                        queues[0]!,
                        queues[1]!);
                    spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
                }
            }
            items = items
                .Where(static item => item.IsValid)
                .GroupBy(static item => item.TypeFullName)
                .Select(static group => group.First())
                .ToImmutableArray();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.Tools.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated Rebus transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // RegisterArkRebusHandlersFromAssembly is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Registers the generated Rebus handler wrappers into the SimpleInjector collection resolved by the Rebus activator. TAssemblyMarker selects the assembly scanned for attributed handlers.</summary>");
            sb.AppendLine("        public static void RegisterArkRebusHandlersFromAssembly<TAssemblyMarker>(global::SimpleInjector.Container container)");
            sb.AppendLine("        {");
            if (!legacyRegistrationItems.IsDefaultOrEmpty)
            {
                foreach (var e in legacyRegistrationItems)
                {
                    sb.AppendLine("            container.Collection.Append(typeof(global::Rebus.Handlers.IHandleMessages<" + e.TypeFullName + ">), typeof(" + e.TypeName + "RebusHandler));");
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Registers handlers selected by ArkGenerateRebusForAssemblyAttribute on TContext.</summary>");
            sb.AppendLine("        public static void RegisterArkRebusHandlers<TContext>(global::SimpleInjector.Container container)");
            sb.AppendLine("        {");
            sb.AppendLine("            RegisterArkRebusHandlersFromAssembly<TContext>(container);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Registers generated owner queues with Rebus type-based routing.</summary>");
            sb.AppendLine("        public static void ConfigureArkRebusRouting<TAssemblyMarker>(global::Rebus.Config.StandardConfigurer<global::Rebus.Routing.IRouter> routing)");
            sb.AppendLine("        {");
            sb.AppendLine("            var typeBased = global::Rebus.Routing.TypeBased.TypeBasedRouterConfigurationExtensions.TypeBased(routing);");
            foreach (var e in items.Where(item => item.OwnerQueue is not null))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine("            typeBased.Map<" + e.TypeFullName + ">(" + StringLiteral(e.OwnerQueue!) + ");");
            }
            sb.AppendLine("        }");

            // Generated Rebus IHandleMessages<T> wrappers.
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var e in items)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    var processorService = e.IsCommand
                        ? "global::Ark.Tools.Solid.ICommandProcessor"
                        : "global::Ark.Tools.Solid.IRequestProcessor";
                    sb.AppendLine();
                    sb.AppendLine("        /// <summary>Generated Rebus wrapper dispatching to the pure handler for <c>" + e.TypeName + "</c>.</summary>");
                    sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.Tools.MediatorFramework.Rebus.Generators\", \"1.0.0\")]");
                    sb.AppendLine("        public sealed class " + e.TypeName + "RebusHandler : global::Rebus.Handlers.IHandleMessages<" + e.TypeFullName + ">");
                    sb.AppendLine("        {");
                    sb.AppendLine("            private readonly " + processorService + " _processor;");
                    sb.AppendLine("            /// <summary>Initializes a new instance.</summary>");
                    sb.AppendLine("            public " + e.TypeName + "RebusHandler(" + processorService + " processor) { _processor = processor; }");
                    sb.AppendLine("            /// <inheritdoc />");
                    sb.AppendLine("            public async global::System.Threading.Tasks.Task Handle(" + e.TypeFullName + " message)");
                    var dispatch = e.IsCommand
                        ? "_processor.ExecuteAsync<" + e.TypeFullName + ">(message, "
                        : "_processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(message, ";
                    sb.AppendLine("                => await " + dispatch + "global::Rebus.Extensions.MessageContextExtensions.GetCancellationToken(global::Rebus.Pipeline.MessageContext.Current)).ConfigureAwait(false);");
                    sb.AppendLine("        }");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.Rebus.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            foreach (var host in hosts)
                EmitHost(spc, host);
        }

        private static void EmitHost(SourceProductionContext spc, HostModel host)
        {
            var retryExpression = host.RetryTypeFullName is null
                ? "global::Ark.Tools.MediatorFramework.Messaging.MessagingDefaultRetryPolicy.Instance"
                : "new " + host.RetryTypeFullName + "()";
            var handlers = host.Adapters
                .AddRange(host.LegacyEndpoints)
                .Where(static endpoint => endpoint.IsValid)
                .GroupBy(static endpoint => endpoint.TypeFullName)
                .Select(static group => group.First())
                .OrderBy(static endpoint => endpoint.TypeFullName, StringComparer.Ordinal)
                .ToImmutableArray();
            var routes = host.Routes
                .AddRange(host.LegacyEndpoints)
                .Where(static endpoint => endpoint.OwnerQueue is not null)
                .GroupBy(static endpoint => endpoint.TypeFullName)
                .Select(static group => group.First())
                .OrderBy(static endpoint => endpoint.TypeFullName, StringComparer.Ordinal)
                .ToImmutableArray();
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            if (!string.IsNullOrEmpty(host.Namespace))
            {
                sb.Append("namespace ").Append(host.Namespace).AppendLine();
                sb.AppendLine("{");
            }
            sb.Append("    ").Append(host.Accessibility).Append(" sealed partial class ").Append(host.Name)
                .AppendLine(" : global::Ark.Tools.MediatorFramework.Rebus.IArkRebusHost");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Registers the generated Rebus handlers and transport-neutral bus for this host.</summary>");
            sb.AppendLine("        public static void Register(global::SimpleInjector.Container container)");
            sb.AppendLine("        {");
            foreach (var handler in handlers)
                sb.AppendLine("            container.Collection.Append(typeof(global::Rebus.Handlers.IHandleMessages<" + handler.TypeFullName + ">), typeof(" + handler.TypeName + "RebusHandler));");
            foreach (var contract in host.Processes)
                sb.AppendLine("            container.Collection.Append(typeof(global::Rebus.Handlers.IHandleMessages<global::Rebus.Retry.Simple.IFailed<" + contract + ">>), typeof(global::Ark.Tools.MediatorFramework.Rebus.RebusMessagingFailedHandler<" + contract + ">));");
            sb.AppendLine("            container.RegisterSingleton<global::Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>(() =>");
            sb.AppendLine("                new global::Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus(");
            sb.AppendLine("                    container.GetInstance<global::Rebus.Bus.IBus>(),");
            sb.AppendLine("                    " + StringLiteral(host.Identity) + ",");
            sb.AppendLine("                    new global::System.Type[]");
            sb.AppendLine("                    {");
            foreach (var contract in host.Publishes)
                sb.AppendLine("                        typeof(" + contract + "),");
            sb.AppendLine("                    }));");
            sb.AppendLine("            container.RegisterSingleton<global::Ark.Tools.MediatorFramework.IBus>(() => container.GetInstance<global::Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>());");
            sb.AppendLine("            container.RegisterSingleton<global::Ark.Tools.MediatorFramework.IBusOutboxEnlistment>(() => container.GetInstance<global::Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>());");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Configures owner queues for this host.</summary>");
            sb.AppendLine("        public static void ConfigureRouting(global::Rebus.Config.StandardConfigurer<global::Rebus.Routing.IRouter> routing)");
            sb.AppendLine("        {");
            sb.AppendLine("            var typeBased = global::Rebus.Routing.TypeBased.TypeBasedRouterConfigurationExtensions.TypeBased(routing);");
            foreach (var route in routes)
                sb.AppendLine("            typeBased.Map<" + route.TypeFullName + ">(" + StringLiteral(route.OwnerQueue!) + ");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Maps this host's retry policy to Rebus options.</summary>");
            sb.AppendLine("        public static void ConfigureOptions(global::Rebus.Config.OptionsConfigurer options)");
            sb.AppendLine("        {");
            sb.AppendLine("            var retry = " + retryExpression + ";");
            sb.AppendLine("            global::Ark.Tools.Rebus.Retry.ArkRetryStrategyConfigurationExtensions.ArkRetryStrategy(");
            sb.AppendLine("                options,");
            sb.AppendLine("                maxDeliveryAttempts: retry.MaximumDeliveryCount,");
            sb.AppendLine("                secondLevelRetriesEnabled: retry.SecondLevelRetriesEnabled);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Subscribes this host to its declared Rebus events.</summary>");
            sb.AppendLine("        public static async global::System.Threading.Tasks.Task SubscribeAsync(");
            sb.AppendLine("            global::Rebus.Bus.IBus bus,");
            sb.AppendLine("            global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            cancellationToken.ThrowIfCancellationRequested();");
            foreach (var contract in host.Subscribes)
            {
                sb.AppendLine("            await global::Rebus.Bus.BusExtensions.Subscribe<" + contract + ">(bus).ConfigureAwait(false);");
                sb.AppendLine("            cancellationToken.ThrowIfCancellationRequested();");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Gets immutable infrastructure requirements for this host.</summary>");
            sb.AppendLine("        public static global::Ark.Tools.MediatorFramework.Rebus.ArkRebusParticipantRequirements GetRequirements()");
            sb.AppendLine("        {");
            sb.AppendLine("            var retry = " + retryExpression + ";");
            sb.AppendLine("            return new global::Ark.Tools.MediatorFramework.Rebus.ArkRebusParticipantRequirements(");
            sb.AppendLine("                " + StringLiteral(host.Identity) + ",");
            sb.AppendLine("                " + (host.Adapters.IsDefaultOrEmpty ? "null" : StringLiteral(host.Identity)) + ",");
            sb.AppendLine("                new global::System.Type[]");
            sb.AppendLine("                {");
            foreach (var contract in host.Publishes)
                sb.AppendLine("                    typeof(" + contract + "),");
            sb.AppendLine("                },");
            sb.AppendLine("                new global::System.Type[]");
            sb.AppendLine("                {");
            foreach (var contract in host.Subscribes)
                sb.AppendLine("                    typeof(" + contract + "),");
            sb.AppendLine("                },");
            sb.AppendLine("                retry.MaximumHandlerDuration,");
            sb.AppendLine("                " + (host.RequiresCompression ? "true" : "false") + ",");
            sb.AppendLine("                " + (host.RequiresDataBus ? "true" : "false") + ");");
            sb.AppendLine("        }");

            foreach (var handler in handlers)
            {
                var processorService = handler.IsCommand
                    ? "global::Ark.Tools.Solid.ICommandProcessor"
                    : "global::Ark.Tools.Solid.IRequestProcessor";
                sb.AppendLine();
                sb.AppendLine("        /// <summary>Generated Rebus wrapper dispatching to the pure application handler.</summary>");
                sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.Tools.MediatorFramework.Rebus.Generators\", \"1.0.0\")]");
                sb.AppendLine("        private sealed class " + handler.TypeName + "RebusHandler : global::Rebus.Handlers.IHandleMessages<" + handler.TypeFullName + ">");
                sb.AppendLine("        {");
                sb.AppendLine("            private readonly " + processorService + " _processor;");
                sb.AppendLine("            public " + handler.TypeName + "RebusHandler(" + processorService + " processor) { _processor = processor; }");
                sb.AppendLine("            public async global::System.Threading.Tasks.Task Handle(" + handler.TypeFullName + " message)");
                var dispatch = handler.IsCommand
                    ? "_processor.ExecuteAsync<" + handler.TypeFullName + ">(message, "
                    : "_processor.ExecuteAsync<" + handler.TypeFullName + ", " + handler.Response + ">(message, ";
                sb.AppendLine("                => await " + dispatch + "global::Rebus.Extensions.MessageContextExtensions.GetCancellationToken(global::Rebus.Pipeline.MessageContext.Current)).ConfigureAwait(false);");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            if (!string.IsNullOrEmpty(host.Namespace))
                sb.AppendLine("}");
            spc.AddSource(
                (string.IsNullOrEmpty(host.Namespace)
                    ? host.Name
                    : host.Namespace.Replace('.', '_') + "_" + host.Name) + ".Rebus.g.cs",
                SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private readonly record struct AssemblyMapping(ImmutableArray<string> AssemblyNames);

        private sealed record HostModel(
            string Namespace,
            string Name,
            string Accessibility,
            string ParticipantTypeFullName,
            string Identity,
            ImmutableArray<string> Processes,
            ImmutableArray<string> Publishes,
            ImmutableArray<string> Subscribes,
            string? RetryTypeFullName,
            bool RequiresCompression,
            bool RequiresDataBus,
            ImmutableArray<EndpointModel> Routes,
            ImmutableArray<EndpointModel> Adapters,
            ImmutableArray<EndpointModel> LegacyEndpoints,
            string? Error,
            Location Location)
        {
            public static HostModel Invalid(string error, Location location)
            {
                return new HostModel(
                    string.Empty,
                    string.Empty,
                    "internal",
                    string.Empty,
                    string.Empty,
                    ImmutableArray<string>.Empty,
                    ImmutableArray<string>.Empty,
                    ImmutableArray<string>.Empty,
                    null,
                    false,
                    false,
                    ImmutableArray<EndpointModel>.Empty,
                    ImmutableArray<EndpointModel>.Empty,
                    ImmutableArray<EndpointModel>.Empty,
                    error,
                    location);
            }
        }

        private readonly record struct EndpointModel
        {
            public EndpointModel(
                string typeFullName,
                string typeName,
                string? response,
                string? ownerQueue,
                IReadOnlyList<DiagnosticInfo> diagnostics,
                bool isCommand,
                Location location)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Response = response;
                OwnerQueue = ownerQueue;
                Diagnostics = diagnostics;
                IsCommand = isCommand;
                Location = location;
                IsValid = diagnostics.Count == 0;
            }

            private EndpointModel(INamedTypeSymbol type, DiagnosticInfo diagnostic)
            {
                TypeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                TypeName = GeneratedName(type);
                Diagnostics = new[] { diagnostic };
                IsCommand = false;
                Location = diagnostic.Location;
                IsValid = false;
            }

            public static EndpointModel Invalid(INamedTypeSymbol type, DiagnosticInfo diagnostic)
                => new(type, diagnostic);

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string? Response { get; }
            public string? OwnerQueue { get; }
            public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }
            public bool IsCommand { get; }
            public Location Location { get; }
            public bool IsValid { get; }
        }

        private readonly record struct DiagnosticInfo
        {
            public DiagnosticInfo(DiagnosticDescriptor descriptor, string typeName, Location location, params object[] arguments)
            {
                Descriptor = descriptor;
                Location = location;
                Arguments = arguments.Length == 0
                    ? new object[] { typeName }
                    : new[] { (object)typeName }.Concat(arguments).ToArray();
            }

            public DiagnosticDescriptor Descriptor { get; }
            public Location Location { get; }
            public object[] Arguments { get; }
        }

        private static string StringLiteral(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string GeneratedName(INamedTypeSymbol type)
        {
            var names = new Stack<string>();
            for (var current = type; current is not null; current = current.ContainingType)
                names.Push(current.Name);
            return string.Join("_", names);
        }
    }
}
