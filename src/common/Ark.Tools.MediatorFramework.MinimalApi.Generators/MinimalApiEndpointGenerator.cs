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
    /// <c>[HttpEndpoint]</c> and emits <c>MapArkEndpoints</c> inside a
    /// <c>partial ArkGeneratedEndpoints</c> class. Only the Minimal API transport is emitted by this
    /// generator; add <c>Ark.Tools.MediatorFramework.Rebus.Generators</c> for Rebus and
    /// <c>Ark.Tools.MediatorFramework.Grpc.Generators</c> for gRPC.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkMinimalApiEndpointGenerator : IIncrementalGenerator
    {
        private const string HttpEndpointAttribute = "Ark.MediatorFramework.HttpEndpointAttribute";

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
            var httpAttr = compilation.GetTypeByMetadataName(HttpEndpointAttribute);
            if (httpAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = httpAttr.ContainingAssembly;
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var http = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, httpAttr));
                    if (http is null)
                        continue;

                    var model = Extract(type, http);
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

        private static EndpointModel? Extract(INamedTypeSymbol type, AttributeData http)
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

            if (http.ConstructorArguments.Length != 2)
                return null;

            var verb = http.ConstructorArguments[0].Value as string;
            var template = http.ConstructorArguments[1].Value as string;
            if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(template))
                return null;

            verb = verb!.ToUpperInvariant();
            var httpIntroducedIn = NamedInt(http, "IntroducedIn", 1);
            var httpRetiredIn = NamedInt(http, "RetiredIn", 0);

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                verb,
                template!,
                response,
                kind,
                httpIntroducedIn,
                httpRetiredIn);
        }

        private static int NamedInt(AttributeData attribute, string name, int defaultValue)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is int value ? value : defaultValue;
        }

        private static void Emit(SourceProductionContext spc, ImmutableArray<EndpointModel> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated Minimal API transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // MapArkEndpoints is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Maps every [HttpEndpoint]-declared handler to a Minimal API endpoint.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkEndpoints(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");

            if (!items.IsDefaultOrEmpty)
            {
                var maxVersion = items.Max(static x => Math.Max(x.HttpIntroducedIn, x.HttpRetiredIn > 0 ? x.HttpRetiredIn - 1 : 1));
                foreach (var e in items)
                {
                    var handlerService = e.Kind == HandlerKind.Query
                        ? "global::Ark.Tools.Solid.IQueryHandler<" + e.TypeFullName + ", " + e.Response + ">"
                        : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                    var bind = e.Verb == "GET" || e.Verb == "DELETE"
                        ? "[global::Microsoft.AspNetCore.Http.AsParameters] "
                        : string.Empty;

                    foreach (var version in ActiveVersions(e, maxVersion))
                    {
                        var map = MapMethod(e.Verb);
                        var template = e.Template.Replace("{version}", version.ToString());
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
                        // (one per version) can partition the endpoints.
                        sb.AppendLine("            }).WithGroupName(" + Literal("v" + version) + ");");
                    }
                }
            }

            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.MinimalApi.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
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
                if (version >= endpoint.HttpIntroducedIn
                    && (endpoint.HttpRetiredIn == 0 || version < endpoint.HttpRetiredIn))
                    yield return version;
        }

        private static string Literal(string value)
            => SyntaxFactory.Literal(value).ToFullString();

        private enum HandlerKind
        {
            None = 0,
            Request = 1,
            Query = 2,
        }

        private readonly struct EndpointModel
        {
            public EndpointModel(string typeFullName, string typeName, string verb, string template, string response, HandlerKind kind, int httpIntroducedIn, int httpRetiredIn)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Verb = verb;
                Template = template;
                Response = response;
                Kind = kind;
                HttpIntroducedIn = httpIntroducedIn;
                HttpRetiredIn = httpRetiredIn;
            }

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string Verb { get; }
            public string Template { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public int HttpIntroducedIn { get; }
            public int HttpRetiredIn { get; }
        }
    }
}
