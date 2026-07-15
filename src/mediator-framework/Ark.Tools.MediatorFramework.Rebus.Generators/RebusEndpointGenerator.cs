// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ark.MediatorFramework.Generators
{
    /// <summary>
    /// Incremental generator that discovers <c>Ark.Tools.Solid</c> requests decorated with
    /// <c>[RebusMessage]</c> and emits <c>RegisterArkRebusHandlers</c> plus the per-request
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
        private static readonly DiagnosticDescriptor ConflictingOwnerQueues = new(
            "ARKMF005", "Conflicting Rebus owner queues",
            "The Rebus message type '{0}' declares conflicting owner queues: {1}", "Rebus",
            DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpoints = context.CompilationProvider
                .SelectMany(static (compilation, _) => GetEndpoints(compilation));

            var collected = endpoints.Collect();

            context.RegisterSourceOutput(collected, static (spc, items) => Emit(spc, items));
        }

        private static ImmutableArray<EndpointModel> GetEndpoints(Compilation compilation)
        {
            var rebusAttr = compilation.GetTypeByMetadataName(RebusMessageAttribute);
            if (rebusAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = rebusAttr.ContainingAssembly;
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var rebusAttributes = attrs
                        .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, rebusAttr))
                        .ToArray();
                    if (rebusAttributes.Length == 0)
                        continue;

                    var model = Extract(type, rebusAttributes);
                    if (model is not null)
                        builder.Add(model.Value);
                }
            }

            return builder.ToImmutable();
        }

        private static IEnumerable<IAssemblySymbol> _relevantAssemblies(Compilation compilation, IAssemblySymbol runtimeAssembly)
        {
            yield return compilation.Assembly;

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
                }
            }
        }

        private static EndpointModel? Extract(INamedTypeSymbol type, IReadOnlyList<AttributeData> rebusAttributes)
        {
            // Rebus messages are dispatched via IRequestHandler; queries (reads) are not
            // meaningful as bus messages.
            foreach (var iface in type.AllInterfaces)
            {
                var def = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def == "global::Ark.Tools.Solid.IRequest<TResponse>")
                {
                    var response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var ownerQueues = rebusAttributes.Select(GetOwnerQueue).ToArray();
                    var diagnostics = new List<DiagnosticInfo>();
                    for (var index = 0; index < ownerQueues.Length; index++)
                    {
                        if (ownerQueues[index] is null && HasOwnerQueueArgument(rebusAttributes[index]))
                        {
                            diagnostics.Add(new DiagnosticInfo(
                                InvalidOwnerQueue,
                                type.Name,
                                GetLocation(rebusAttributes[index])));
                        }
                    }

                    var distinctOwnerQueues = ownerQueues
                        .Where(ownerQueue => !string.IsNullOrWhiteSpace(ownerQueue))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (distinctOwnerQueues.Length > 1)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            ConflictingOwnerQueues,
                            type.Name,
                            GetLocation(rebusAttributes[0]),
                            string.Join(", ", distinctOwnerQueues)));
                    }

                    return new EndpointModel(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        type.Name,
                        response,
                        distinctOwnerQueues.Length == 1 && diagnostics.Count == 0 ? distinctOwnerQueues[0] : null,
                        diagnostics);
                }
            }

            return null;
        }

        private static string? GetOwnerQueue(AttributeData attribute)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "OwnerQueue");
            var ownerQueue = argument.Value.Value as string;
            return string.IsNullOrWhiteSpace(ownerQueue) ? null : ownerQueue;
        }

        private static bool HasOwnerQueueArgument(AttributeData attribute)
            => attribute.NamedArguments.Any(pair => pair.Key == "OwnerQueue");

        private static Location GetLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        private static void Emit(SourceProductionContext spc, ImmutableArray<EndpointModel> items)
        {
            if (items.IsDefaultOrEmpty)
                return;

            foreach (var item in items)
            {
                foreach (var diagnostic in item.Diagnostics)
                    spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
            }

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated Rebus transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // RegisterArkRebusHandlers is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Registers the generated Rebus handler wrappers into the SimpleInjector collection resolved by the Rebus activator.</summary>");
            sb.AppendLine("        public static void RegisterArkRebusHandlers(global::SimpleInjector.Container container)");
            sb.AppendLine("        {");
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var e in items)
                {
                    sb.AppendLine("            container.Collection.Append(typeof(global::Rebus.Handlers.IHandleMessages<" + e.TypeFullName + ">), typeof(" + e.TypeName + "RebusHandler));");
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Registers generated owner queues with Rebus type-based routing.</summary>");
            sb.AppendLine("        public static void ConfigureArkRebusRouting(global::Rebus.Config.StandardConfigurer<global::Rebus.Routing.IRouter> routing)");
            sb.AppendLine("        {");
            sb.AppendLine("            var typeBased = global::Rebus.Routing.TypeBased.TypeBasedRouterConfigurationExtensions.TypeBased(routing);");
            foreach (var e in items.Where(item => item.OwnerQueue is not null))
            {
                sb.AppendLine("            typeBased.Map<" + e.TypeFullName + ">(" + StringLiteral(e.OwnerQueue!) + ");");
            }
            sb.AppendLine("        }");

            // Generated Rebus IHandleMessages<T> wrappers.
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var e in items)
                {
                    var handlerService = "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
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
                    sb.AppendLine("                => await _handler.ExecuteAsync(message).ConfigureAwait(false);");
                    sb.AppendLine("        }");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.Rebus.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private readonly struct EndpointModel
        {
            public EndpointModel(
                string typeFullName,
                string typeName,
                string response,
                string? ownerQueue,
                IReadOnlyList<DiagnosticInfo> diagnostics)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Response = response;
                OwnerQueue = ownerQueue;
                Diagnostics = diagnostics;
            }

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string Response { get; }
            public string? OwnerQueue { get; }
            public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }
        }

        private readonly struct DiagnosticInfo
        {
            public DiagnosticInfo(DiagnosticDescriptor descriptor, string typeName, Location location, string? queues = null)
            {
                Descriptor = descriptor;
                Location = location;
                Arguments = queues is null ? [typeName] : [typeName, queues];
            }

            public DiagnosticDescriptor Descriptor { get; }
            public Location Location { get; }
            public object[] Arguments { get; }
        }

        private static string StringLiteral(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
