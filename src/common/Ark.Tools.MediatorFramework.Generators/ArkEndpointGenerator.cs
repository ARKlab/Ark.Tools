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
    /// Incremental generator that discovers pure <c>Ark.Tools.Solid</c> requests/queries and emits
    /// the transport hosting code (ASP.NET Core Minimal API mappings and Rebus message-handler
    /// wrappers) at compile time. This removes the MVC runtime-reflection tax while keeping handlers
    /// transport-agnostic.
    /// </summary>
    /// <remarks>
    /// Transport declaration is explicit and opt-in per transport: a Minimal API endpoint is emitted
    /// only for types marked <c>[HttpEndpoint]</c>, a Rebus wrapper only for <c>[RebusMessage]</c>,
    /// and a gRPC method only for <c>[GrpcMethod]</c>. A request/query with no transport attribute is
    /// ignored, letting a developer hand-write the mapping when the framework is too limited.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkEndpointGenerator : IIncrementalGenerator
    {
        private const string HttpEndpointAttribute = "Ark.MediatorFramework.HttpEndpointAttribute";
        private const string RebusMessageAttribute = "Ark.MediatorFramework.RebusMessageAttribute";
        private const string GrpcMethodAttribute = "Ark.MediatorFramework.GrpcMethodAttribute";
        private const string ServiceGroupAttribute = "Ark.MediatorFramework.ServiceGroupAttribute";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // ponytail: discovery walks the current assembly plus any referenced assembly that
            // references the runtime (attribute) assembly, on every compilation. This lets the
            // *hosting* assembly emit endpoints for contracts declared in the *application* assembly.
            // Upgrade path: cache per metadata-reference if generation cost ever matters.
            var endpoints = context.CompilationProvider
                .SelectMany(static (compilation, _) => GetEndpoints(compilation));

            var collected = endpoints.Collect();

            context.RegisterSourceOutput(collected, static (spc, items) => Emit(spc, items));
        }

        private static ImmutableArray<EndpointModel> GetEndpoints(Compilation compilation)
        {
            var httpAttr = compilation.GetTypeByMetadataName(HttpEndpointAttribute);
            var rebusAttr = compilation.GetTypeByMetadataName(RebusMessageAttribute);
            var grpcAttr = compilation.GetTypeByMetadataName(GrpcMethodAttribute);
            var serviceGroupAttr = compilation.GetTypeByMetadataName(ServiceGroupAttribute);
            if (httpAttr is null && rebusAttr is null && grpcAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = (httpAttr ?? rebusAttr ?? grpcAttr)!.ContainingAssembly;
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var http = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, httpAttr));
                    var rebus = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, rebusAttr));
                    var grpc = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, grpcAttr));
                    if (http is null && rebus is null && grpc is null)
                        continue;

                    var serviceGroup = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serviceGroupAttr));
                    var model = Extract(type, http, rebus is not null, grpc, serviceGroup);
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

        private static EndpointModel? Extract(INamedTypeSymbol type, AttributeData? http, bool hasRebus, AttributeData? grpc, AttributeData? serviceGroup)
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

            string? verb = null;
            string? template = null;
            var introducedIn = 1;
            var retiredIn = 0;
            if (http is not null && http.ConstructorArguments.Length == 2)
            {
                verb = http.ConstructorArguments[0].Value as string;
                template = http.ConstructorArguments[1].Value as string;
                if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(template))
                {
                    verb = null;
                    template = null;
                }
                else
                {
                    verb = verb!.ToUpperInvariant();
                    introducedIn = NamedInt(http, "IntroducedIn", 1);
                    retiredIn = NamedInt(http, "RetiredIn", 0);
                }
            }

            var hasHttp = verb is not null && template is not null;
            var grpcMethod = grpc?.ConstructorArguments.FirstOrDefault().Value as string ?? (grpc is null ? null : type.Name);
            if (grpc is not null)
            {
                introducedIn = NamedInt(grpc, "IntroducedIn", 1);
                retiredIn = NamedInt(grpc, "RetiredIn", 0);
            }
            var group = serviceGroup?.ConstructorArguments.FirstOrDefault().Value as string ?? "Ark";
            if (!hasHttp && !hasRebus && grpcMethod is null)
                return null;

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                verb,
                template,
                response,
                kind,
                hasRebus,
                grpcMethod,
                group,
                introducedIn,
                retiredIn);
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
            sb.AppendLine("    /// <summary>Source-generated transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.Generators\", \"1.0.0\")]");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // Minimal API registration (opt-in via [HttpEndpoint]).
            sb.AppendLine("        /// <summary>Maps every [HttpEndpoint]-declared handler to a Minimal API endpoint.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkEndpoints(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");
            var maxVersion = items.Length == 0
                ? 1
                : items.Max(static x => Math.Max(x.IntroducedIn, x.RetiredIn > 0 ? x.RetiredIn - 1 : 1));
            foreach (var e in items.Where(static x => x.Verb is not null))
            {
                var handlerService = e.Kind == HandlerKind.Query
                    ? "global::Ark.Tools.Solid.IQueryHandler<" + e.TypeFullName + ", " + e.Response + ">"
                    : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                var bind = e.Verb == "GET" || e.Verb == "DELETE"
                    ? "[global::Microsoft.AspNetCore.Http.AsParameters] "
                    : string.Empty;

                foreach (var version in ActiveVersions(e, maxVersion))
                {
                    var map = MapMethod(e.Verb!);
                    var template = e.Template!.Replace("{version}", version.ToString());
                    sb.AppendLine("            app." + map + "(" + Literal(template) + ", static async (");
                    sb.AppendLine("                " + bind + e.TypeFullName + " request,");
                    sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
                    sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
                    sb.AppendLine("            {");
                // The SimpleInjector async scope spans the whole request (established by the hosting
                // pipeline); the endpoint resolves the handler from that ambient scope.
                sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
                sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.Ok(result);");

                // Route-based API versioning: the version segment of the template (for example
                // "/api/v2/...") becomes the endpoint group name, so multiple OpenAPI documents
                // (one per version) can partition the endpoints. Endpoints with no version segment
                // stay ungrouped and therefore appear in every document.
                    sb.AppendLine("            }).WithGroupName(" + Literal("v" + version) + ");");
                }
            }
            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Code-first gRPC services (opt-in via [GrpcMethod]).
            foreach (var group in items.Where(static x => x.GrpcMethod is not null).GroupBy(static x => x.ServiceGroup))
            {
                for (var version = 1; version <= maxVersion; version++)
                {
                    var active = group.Where(e => IsActive(e, version)).ToArray();
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
                    sb.AppendLine("            [global::System.ServiceModel.OperationContract(Name = " + Literal(e.GrpcMethod!) + ")]");
                    sb.AppendLine("            global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                }
                    sb.AppendLine("        }");
                sb.AppendLine();
                    sb.AppendLine("        /// <summary>Generated partial gRPC implementation for the " + Escape(group.Key) + " v" + version + " group.</summary>");
                sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.Generators\", \"1.0.0\")]");
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

            sb.AppendLine("        /// <summary>Maps every generated code-first gRPC service.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkGrpcServices(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");
            foreach (var group in items.Where(static x => x.GrpcMethod is not null).GroupBy(static x => x.ServiceGroup))
                for (var version = 1; version <= maxVersion; version++)
                    if (group.Any(e => IsActive(e, version)))
                        sb.AppendLine("            global::Microsoft.AspNetCore.Builder.GrpcEndpointRouteBuilderExtensions.MapGrpcService<" + Identifier(group.Key) + "V" + version + "GrpcService>(app);");
            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Rebus handler registration (opt-in via [RebusMessage]; requests only, queries are reads).
            sb.AppendLine("        /// <summary>Registers the generated Rebus handler wrappers into the SimpleInjector collection resolved by the Rebus activator.</summary>");
            sb.AppendLine("        public static void RegisterArkRebusHandlers(global::SimpleInjector.Container container)");
            sb.AppendLine("        {");
            foreach (var e in items.Where(static x => x.HasRebus && x.Kind == HandlerKind.Request))
            {
                sb.AppendLine("            container.Collection.Append(typeof(global::Rebus.Handlers.IHandleMessages<" + e.TypeFullName + ">), typeof(" + e.TypeName + "RebusHandler));");
            }
            sb.AppendLine("        }");

            // Generated Rebus wrappers.
            foreach (var e in items.Where(static x => x.HasRebus && x.Kind == HandlerKind.Request))
            {
                var handlerService = "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                sb.AppendLine();
                sb.AppendLine("        /// <summary>Generated Rebus wrapper dispatching to the pure handler for <c>" + e.TypeName + "</c>.</summary>");
                sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.Generators\", \"1.0.0\")]");
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

            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string MapMethod(string verb) => verb switch
        {
            "GET" => "MapGet",
            "POST" => "MapPost",
            "PUT" => "MapPut",
            "DELETE" => "MapDelete",
            "PATCH" => "MapPatch",
            _ => "MapPost",
        };

        private static IEnumerable<int> ActiveVersions(EndpointModel endpoint, int maxVersion)
        {
            for (var version = 1; version <= maxVersion; version++)
                if (IsActive(endpoint, version))
                    yield return version;
        }

        private static bool IsActive(EndpointModel endpoint, int version)
        {
            return version >= endpoint.IntroducedIn
                && (endpoint.RetiredIn == 0 || version < endpoint.RetiredIn);
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
            public EndpointModel(string typeFullName, string typeName, string? verb, string? template, string response, HandlerKind kind, bool hasRebus, string? grpcMethod, string serviceGroup, int introducedIn, int retiredIn)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Verb = verb;
                Template = template;
                Response = response;
                Kind = kind;
                HasRebus = hasRebus;
                GrpcMethod = grpcMethod;
                ServiceGroup = serviceGroup;
                IntroducedIn = introducedIn;
                RetiredIn = retiredIn;
            }

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string? Verb { get; }
            public string? Template { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public bool HasRebus { get; }
            public string? GrpcMethod { get; }
            public string ServiceGroup { get; }
            public int IntroducedIn { get; }
            public int RetiredIn { get; }
        }
    }
}
