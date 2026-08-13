// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ark.MediatorFramework.Generators
{
    /// <summary>
    /// Incremental generator that discovers <c>Ark.Tools.Solid</c> requests decorated with
    /// <c>[RebusMessage]</c> and emits <c>RegisterArkRebusHandlersFromAssembly</c> plus the per-request
    /// <c>IHandleMessages&lt;T&gt;</c> wrapper classes inside a <c>partial ArkGeneratedEndpoints</c>
    /// class. Only the Rebus transport is emitted by this generator; add
    /// <c>Ark.Tools.MediatorFramework.MinimalApi.Generators</c> for HTTP and
    /// <c>Ark.Tools.MediatorFramework.Grpc.Generators</c> for gRPC.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkRebusEndpointGenerator : IIncrementalGenerator
    {
        private const string RebusMessageAttribute = "Ark.MediatorFramework.RebusMessageAttribute";
        private static readonly DiagnosticDescriptor InvalidOwnerQueue = new(
            "ARKMF004", "Invalid Rebus owner queue",
            "The Rebus owner queue for '{0}' must not be blank", "Rebus",
            DiagnosticSeverity.Error, isEnabledByDefault: true);
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpointAssemblies = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is InvocationExpressionSyntax,
                    static (syntaxContext, cancellationToken) =>
                        GetAssemblyName(syntaxContext, cancellationToken))
                .Where(static assemblyName => assemblyName is not null)
                .Select(static (assemblyName, _) => assemblyName!)
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

            var sourceWithFailedHandlers = context.CompilationProvider
                .Combine(sourceEndpoints.Collect())
                .Select(static (pair, cancellationToken) =>
                {
                    var failedHandlers = FailedHandlers(pair.Left.Assembly, cancellationToken);
                    return pair.Right
                        .Select(endpoint => endpoint.WithFailedHandlers(failedHandlers))
                        .ToImmutableArray();
                });
            var collected = sourceWithFailedHandlers.Combine(referencedEndpoints.Collect());

            context.RegisterSourceOutput(
                collected,
                static (spc, pair) => Emit(spc, pair.Left.AddRange(pair.Right)));
        }

        private static EndpointModel? ExtractSourceEndpoint(GeneratorAttributeSyntaxContext context)
        {
            return Extract((INamedTypeSymbol)context.TargetSymbol, context.Attributes[0]);
        }

        private static string? GetAssemblyName(
            GeneratorSyntaxContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = (InvocationExpressionSyntax)context.Node;
            var method = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var genericName = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .FirstOrDefault(name => name.Identifier.ValueText is "RegisterArkRebusHandlersFromAssembly" or "ConfigureArkRebusRouting");
            if (genericName is null || genericName.TypeArgumentList.Arguments.Count != 1)
                return null;
            if (method is not null
                ? !string.Equals(method.MetadataName, genericName.Identifier.ValueText, StringComparison.Ordinal)
                : !IsGeneratedEndpointInvocation(invocation, genericName.Identifier.ValueText))
                return null;

            return context.SemanticModel
                .GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken)
                .Type?.ContainingAssembly?.Name;
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
                var failedHandlers = FailedHandlers(assembly, cancellationToken);
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
                        builder.Add(model.Value.WithFailedHandlers(failedHandlers));
                }
            }

            return builder.ToImmutable();
        }

        private static IReadOnlyList<FailedHandlerModel> FailedHandlers(
            IAssemblySymbol assembly,
            CancellationToken cancellationToken)
        {
            var handlers = new List<FailedHandlerModel>();
            foreach (var type in _allTypes(assembly.GlobalNamespace))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (type.TypeKind != TypeKind.Class
                    || type.IsAbstract
                    || type.DeclaredAccessibility != Accessibility.Public)
                    continue;

                foreach (var iface in type.AllInterfaces)
                {
                    if (!IsType(iface.OriginalDefinition, "IHandleMessages`1", "Rebus.Handlers")
                        || iface.TypeArguments.Length != 1)
                        continue;

                    if (iface.TypeArguments[0] is not INamedTypeSymbol failed
                        || !IsType(failed.OriginalDefinition, "IFailed`1", "Rebus.Retry.Simple")
                        || failed.TypeArguments.Length != 1)
                        continue;

                    handlers.Add(new FailedHandlerModel(
                        iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
            }

            return handlers;
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

        private static void Emit(SourceProductionContext spc, ImmutableArray<EndpointModel> items)
        {
            if (items.IsDefaultOrEmpty)
                return;

            items = items.OrderBy(static item => item.TypeFullName, StringComparer.Ordinal).ToImmutableArray();
            foreach (var item in items)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                foreach (var diagnostic in item.Diagnostics)
                    spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
            }
            foreach (var group in items.Where(static item => item.IsValid).GroupBy(static item => item.TypeFullName))
            {
                var validItems = group.ToArray();
                if (validItems.Length < 2)
                    continue;

                var queues = validItems.Select(static item => item.OwnerQueue).Distinct(StringComparer.Ordinal).ToArray();
                var descriptor = queues.Length > 1
                    ? DiagnosticDescriptors.ConflictingOwnerQueue
                    : DiagnosticDescriptors.DuplicateRegistration;
                foreach (var item in validItems)
                {
                    var diagnostic = queues.Length > 1
                        ? new DiagnosticInfo(descriptor, item.TypeName, item.Location, queues[0]!, queues[1]!)
                        : new DiagnosticInfo(descriptor, item.TypeName, item.Location);
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
            sb.AppendLine("namespace Ark.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated Rebus transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // RegisterArkRebusHandlersFromAssembly is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Registers the generated Rebus handler wrappers into the SimpleInjector collection resolved by the Rebus activator. TAssemblyMarker selects the assembly scanned for attributed handlers.</summary>");
            sb.AppendLine("        public static void RegisterArkRebusHandlersFromAssembly<TAssemblyMarker>(global::SimpleInjector.Container container)");
            sb.AppendLine("        {");
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var e in items)
                {
                    sb.AppendLine("            container.Collection.Append(typeof(global::Rebus.Handlers.IHandleMessages<" + e.TypeFullName + ">), typeof(" + e.TypeName + "RebusHandler));");
                }
            }
            foreach (var handler in items
                .SelectMany(static item => item.FailedHandlers)
                .Distinct()
                .OrderBy(static handler => handler.HandlerTypeFullName, StringComparer.Ordinal))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine("            container.Collection.Append(typeof(" + handler.InterfaceTypeFullName + "), typeof(" + handler.HandlerTypeFullName + "));");
            }
            sb.AppendLine("            var missingHandlers = new global::System.Collections.Generic.List<string>();");
            foreach (var handler in items
                .Select(HandlerService)
                .Distinct(StringComparer.Ordinal))
            {
                var contract = items
                    .First(item => HandlerService(item) == handler)
                    .TypeFullName;
                sb.AppendLine("            VerifyRebusHandlerRegistration(container, typeof(" + handler + "), " + StringLiteral(contract) + ", missingHandlers);");
            }
            sb.AppendLine("            if (missingHandlers.Count > 0)");
            sb.AppendLine("                throw new global::System.InvalidOperationException(\"Missing mediator handler registrations: \" + string.Join(\"; \", missingHandlers));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void VerifyRebusHandlerRegistration(global::SimpleInjector.Container container, global::System.Type handlerType, string contract, global::System.Collections.Generic.List<string> missingHandlers)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (container.GetRegistration(handlerType) is null)");
            sb.AppendLine("                missingHandlers.Add(contract + \" -> \" + handlerType);");
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
                    var handlerService = e.IsCommand
                        ? "global::Ark.Tools.Solid.ICommandHandler<" + e.TypeFullName + ">"
                        : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                    sb.AppendLine();
                    sb.AppendLine("        /// <summary>Generated Rebus wrapper dispatching to the pure handler for <c>" + e.TypeName + "</c>.</summary>");
                    sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.Rebus.Generators\", \"1.0.0\")]");
                    sb.AppendLine("        public sealed class " + e.TypeName + "RebusHandler : global::Rebus.Handlers.IHandleMessages<" + e.TypeFullName + ">");
                    sb.AppendLine("        {");
                    sb.AppendLine("            private readonly " + handlerService + " _handler;");
                    sb.AppendLine("            /// <summary>Initializes a new instance.</summary>");
                    sb.AppendLine("            public " + e.TypeName + "RebusHandler(" + handlerService + " handler) { _handler = handler; }");
                    sb.AppendLine("            /// <inheritdoc />");
                    sb.AppendLine("            public async global::System.Threading.Tasks.Task Handle(" + e.TypeFullName + " message)");
                    sb.AppendLine("                => await _handler.ExecuteAsync(message, global::Rebus.Extensions.MessageContextExtensions.GetCancellationToken(global::Rebus.Pipeline.MessageContext.Current)).ConfigureAwait(false);");
                    sb.AppendLine("        }");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.Rebus.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string HandlerService(EndpointModel item)
        {
            return item.IsCommand
                ? "global::Ark.Tools.Solid.ICommandHandler<" + item.TypeFullName + ">"
                : "global::Ark.Tools.Solid.IRequestHandler<" + item.TypeFullName + ", " + item.Response + ">";
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
                Location location,
                IReadOnlyList<FailedHandlerModel>? failedHandlers = null)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Response = response;
                OwnerQueue = ownerQueue;
                Diagnostics = diagnostics;
                IsCommand = isCommand;
                Location = location;
                IsValid = diagnostics.Count == 0;
                FailedHandlers = failedHandlers ?? Array.Empty<FailedHandlerModel>();
            }

            private EndpointModel(INamedTypeSymbol type, DiagnosticInfo diagnostic)
            {
                TypeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                TypeName = GeneratedName(type);
                Diagnostics = new[] { diagnostic };
                IsCommand = false;
                Location = diagnostic.Location;
                IsValid = false;
                FailedHandlers = Array.Empty<FailedHandlerModel>();
            }

            public static EndpointModel Invalid(INamedTypeSymbol type, DiagnosticInfo diagnostic)
                => new(type, diagnostic);

            public EndpointModel WithFailedHandlers(IReadOnlyList<FailedHandlerModel> failedHandlers)
                => new(
                    TypeFullName,
                    TypeName,
                    Response,
                    OwnerQueue,
                    Diagnostics,
                    IsCommand,
                    Location,
                    failedHandlers);

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string? Response { get; }
            public string? OwnerQueue { get; }
            public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }
            public bool IsCommand { get; }
            public Location Location { get; }
            public bool IsValid { get; }
            public IReadOnlyList<FailedHandlerModel> FailedHandlers { get; }
        }

        private readonly record struct FailedHandlerModel(
            string InterfaceTypeFullName,
            string HandlerTypeFullName);

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
