// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ark.MediatorFramework.Generators
{
    /// <summary>
    /// Incremental generator that discovers <c>Ark.Tools.Solid</c> requests/queries decorated with
    /// <c>[GrpcMethod]</c> and emits code-first gRPC service contracts plus <c>MapArkGrpcServices</c>
    /// inside a <c>partial ArkGeneratedEndpoints</c> class. Only the gRPC transport is emitted by
    /// this generator; add <c>Ark.Tools.MediatorFramework.MinimalApi.Generators</c> for HTTP and
    /// <c>Ark.Tools.MediatorFramework.Rebus.Generators</c> for Rebus.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkGrpcEndpointGenerator : IIncrementalGenerator
    {
        private const string GrpcMethodAttribute = "Ark.MediatorFramework.GrpcMethodAttribute";
        private const string ServiceGroupAttribute = "Ark.MediatorFramework.ServiceGroupAttribute";

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
            var grpcAttr = compilation.GetTypeByMetadataName(GrpcMethodAttribute);
            var serviceGroupAttr = compilation.GetTypeByMetadataName(ServiceGroupAttribute);
            if (grpcAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = grpcAttr.ContainingAssembly;
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var grpc = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, grpcAttr));
                    if (grpc is null)
                        continue;

                    var serviceGroup = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serviceGroupAttr));
                    var model = Extract(type, grpc, serviceGroup);
                    if (model is not null)
                        builder.Add(model.Value);
                }
            }

            return builder.ToImmutable();
        }

        private static IEnumerable<IAssemblySymbol> _relevantAssemblies(Compilation compilation, IAssemblySymbol runtimeAssembly)
        {
            yield return compilation.Assembly;

            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(reference, runtimeAssembly))
                    continue;

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

        private static EndpointModel? Extract(INamedTypeSymbol type, AttributeData grpc, AttributeData? serviceGroup)
        {
            string? response = null;
            var kind = HandlerKind.None;

            foreach (var iface in type.AllInterfaces)
            {
                var def = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def == "global::Ark.Tools.Solid.IRequest<TResponse>")
                {
                    kind = HandlerKind.Request;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    break;
                }

                if (def == "global::Ark.Tools.Solid.IQuery<TResult>")
                {
                    kind = HandlerKind.Query;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    break;
                }
            }

            if (kind == HandlerKind.None || response is null)
                return null;

            var grpcMethod = grpc.ConstructorArguments.FirstOrDefault().Value as string ?? type.Name;
            var grpcIntroducedIn = NamedInt(grpc, "IntroducedIn", 1);
            var grpcRetiredIn = NamedInt(grpc, "RetiredIn", 0);
            var group = serviceGroup?.ConstructorArguments.FirstOrDefault().Value as string ?? "Ark";

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                grpcMethod,
                group,
                response,
                kind,
                grpcIntroducedIn,
                grpcRetiredIn);
        }

        private static int NamedInt(AttributeData attribute, string name, int defaultValue)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is int value ? value : defaultValue;
        }

        private static void Emit(SourceProductionContext spc, ImmutableArray<EndpointModel> items)
        {
            if (items.IsDefaultOrEmpty)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated code-first gRPC transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            var maxVersion = items.IsDefaultOrEmpty
                ? 1
                : items.Max(static x => Math.Max(x.GrpcIntroducedIn, x.GrpcRetiredIn > 0 ? x.GrpcRetiredIn - 1 : 1));

            // Code-first gRPC service contracts (opt-in via [GrpcMethod]).
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var group in items.GroupBy(static x => x.ServiceGroup))
                {
                    for (var version = 1; version <= maxVersion; version++)
                    {
                        var active = group.Where(e => IsGrpcActive(e, version)).ToArray();
                        if (active.Length == 0)
                            continue;

                        var identifier = Identifier(group.Key) + "V" + version;
                        sb.AppendLine("        /// <summary>Generated code-first gRPC service contract for the " + Escape(group.Key) + " v" + version + " group.</summary>");
                        sb.AppendLine("        [global::System.ServiceModel.ServiceContract(Name = " + Literal(group.Key + "V" + version) + ")]");
                        sb.AppendLine("        public interface I" + identifier + "GrpcService");
                        sb.AppendLine("        {");
                        foreach (var e in active)
                        {
                            sb.AppendLine("            /// <summary>Dispatches " + e.TypeName + " to its pure handler.</summary>");
                            sb.AppendLine("            [global::System.ServiceModel.OperationContract(Name = " + Literal(e.GrpcMethod) + ")]");
                            sb.AppendLine("            global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                        }
                        sb.AppendLine("        }");
                        sb.AppendLine();
                        sb.AppendLine("        /// <summary>Generated partial gRPC implementation for the " + Escape(group.Key) + " v" + version + " group.</summary>");
                        sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.Grpc.Generators\", \"1.0.0\")]");
                        sb.AppendLine("        public sealed partial class " + identifier + "GrpcService : I" + identifier + "GrpcService");
                        sb.AppendLine("        {");
                        sb.AppendLine("            private readonly global::SimpleInjector.Container _container;");
                        sb.AppendLine("            /// <summary>Initializes a new instance.</summary>");
                        sb.AppendLine("            public " + identifier + "GrpcService(global::SimpleInjector.Container container) { _container = container; }");
                        foreach (var e in active)
                        {
                            var handlerService = e.Kind == HandlerKind.Query
                                ? "global::Ark.Tools.Solid.IQueryHandler<" + e.TypeFullName + ", " + e.Response + ">"
                                : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                            sb.AppendLine("            /// <inheritdoc />");
                            sb.AppendLine("            public async global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            sb.AppendLine("            {");
                            sb.AppendLine("                var handler = _container.GetInstance<" + handlerService + ">();");
                            sb.AppendLine("                return await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
                            sb.AppendLine("            }");
                        }
                        sb.AppendLine("        }");
                        sb.AppendLine();
                    }
                }
            }

            // MapArkGrpcServices is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Maps every generated code-first gRPC service.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkGrpcServices(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var group in items.GroupBy(static x => x.ServiceGroup))
                    for (var version = 1; version <= maxVersion; version++)
                        if (group.Any(e => IsGrpcActive(e, version)))
                            sb.AppendLine("            global::Microsoft.AspNetCore.Builder.GrpcEndpointRouteBuilderExtensions.MapGrpcService<" + Identifier(group.Key) + "V" + version + "GrpcService>(app);");
            }
            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.Grpc.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static bool IsGrpcActive(EndpointModel endpoint, int version)
        {
            return version >= endpoint.GrpcIntroducedIn
                && (endpoint.GrpcRetiredIn == 0 || version < endpoint.GrpcRetiredIn);
        }

        private static string Literal(string value)
            => SyntaxFactory.Literal(value).ToFullString();

        private static string Identifier(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var character in value)
                sb.Append(char.IsLetterOrDigit(character) ? character : '_');
            return sb.Length == 0 ? "Ark" : sb.ToString();
        }

        private static string Escape(string value)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private enum HandlerKind
        {
            None = 0,
            Request = 1,
            Query = 2,
        }

        private readonly struct EndpointModel
        {
            public EndpointModel(string typeFullName, string typeName, string grpcMethod, string serviceGroup, string response, HandlerKind kind, int grpcIntroducedIn, int grpcRetiredIn)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                GrpcMethod = grpcMethod;
                ServiceGroup = serviceGroup;
                Response = response;
                Kind = kind;
                GrpcIntroducedIn = grpcIntroducedIn;
                GrpcRetiredIn = grpcRetiredIn;
            }

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string GrpcMethod { get; }
            public string ServiceGroup { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public int GrpcIntroducedIn { get; }
            public int GrpcRetiredIn { get; }
        }
    }
}
