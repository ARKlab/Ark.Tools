// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string BindFromQueryAttribute = "Ark.MediatorFramework.BindFromQueryAttribute";
        private const string ArkAttachment = "Ark.MediatorFramework.IArkAttachment";
        private static readonly DiagnosticDescriptor MultipleAttachments = new DiagnosticDescriptor(
            "ARKMF001",
            "Only one attachment is supported",
            "HTTP endpoint '{0}' declares more than one IArkAttachment property",
            "Ark.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

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
            var bindFromQueryAttr = compilation.GetTypeByMetadataName(BindFromQueryAttribute);
            var attachmentType = compilation.GetTypeByMetadataName(ArkAttachment);
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var http = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, httpAttr));
                    if (http is null)
                        continue;

                    var model = Extract(type, http, bindFromQueryAttr, attachmentType);
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

        private static EndpointModel? Extract(
            INamedTypeSymbol type,
            AttributeData http,
            INamedTypeSymbol? bindFromQueryAttr,
            INamedTypeSymbol? attachmentType)
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
            var acceptsMessagePack = NamedBool(http, "AcceptsMessagePack");
            var policy = NamedString(http, "Policy");
            var allowAnonymous = NamedBool(http, "AllowAnonymous");
            var routeNames = new HashSet<string>(
                Regex.Matches(template!, "\\{([^}:]+)(?::[^}]+)?\\}")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Where(name => !string.Equals(name, "version", StringComparison.OrdinalIgnoreCase))
                , StringComparer.OrdinalIgnoreCase);
            var properties = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
                .Select(property => new PropertyModel(
                    property.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    routeNames.Contains(property.Name),
                    routeNames.FirstOrDefault(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) ?? property.Name,
                    bindFromQueryAttr is not null && property.GetAttributes().Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, bindFromQueryAttr))))
                .ToImmutableArray();
            var attachmentProperties = attachmentType is null
                ? ImmutableArray<PropertyModel>.Empty
                : properties.Where(property => property.TypeFullName == "global::Ark.MediatorFramework.IArkAttachment").ToImmutableArray();

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                verb,
                template!,
                response,
                kind,
                httpIntroducedIn,
                httpRetiredIn,
                acceptsMessagePack,
                policy,
                allowAnonymous,
                properties,
                attachmentProperties.Length,
                type.Locations.FirstOrDefault());
        }

        private static int NamedInt(AttributeData attribute, string name, int defaultValue)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is int value ? value : defaultValue;
        }

        private static bool NamedBool(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is true;
        }

        private static string? NamedString(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value as string;
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
            sb.AppendLine("    /// <summary>Source-generated Minimal API transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.MinimalApi.Generators\", \"1.0.0\")]");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // MapArkEndpoints is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Maps every [HttpEndpoint]-declared handler to a Minimal API endpoint.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.RouteGroupBuilder MapArkEndpoints(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, global::System.Action<global::Microsoft.AspNetCore.Routing.RouteGroupBuilder>? configure = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            var group = global::Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions.MapGroup(endpoints, string.Empty);");

            if (!items.IsDefaultOrEmpty)
            {
                var messagePackEndpoints = items
                    .Where(static endpoint => endpoint.AcceptsMessagePack)
                    .Select(static endpoint => endpoint.TypeFullName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (messagePackEndpoints.Length > 0)
                {
                    sb.Append("            global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ValidateMessagePackContracts(endpoints.ServiceProvider, ");
                    sb.Append(string.Join(
                        ", ",
                        messagePackEndpoints.Select(static type =>
                            "static resolver => global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ValidateMessagePackFormatter<"
                            + type + ">(resolver)")));
                    sb.AppendLine(");");
                }

                var maxVersion = items.Max(static x => Math.Max(x.HttpIntroducedIn, x.HttpRetiredIn > 0 ? x.HttpRetiredIn - 1 : 1));
                foreach (var e in items)
                {
                    if (e.AttachmentCount > 1)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            MultipleAttachments,
                            e.Location,
                            e.TypeName));
                        continue;
                    }

                    var handlerService = e.Kind == HandlerKind.Query
                        ? "global::Ark.Tools.Solid.IQueryHandler<" + e.TypeFullName + ", " + e.Response + ">"
                        : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                    var bind = e.Verb == "GET" || e.Verb == "DELETE"
                        ? "[global::Microsoft.AspNetCore.Http.AsParameters] "
                        : string.Empty;
                    var bodyVerb = e.Verb != "GET" && e.Verb != "DELETE";
                    var explicitBindings = bodyVerb && e.Properties.Any(property => property.IsRoute || property.IsQuery);

                    foreach (var version in ActiveVersions(e, maxVersion))
                    {
                        var map = MapMethod(e.Verb);
                        var template = e.Template.Replace("{version}", version.ToString());
                        if (e.AttachmentCount == 1)
                        {
                            EmitMultipartEndpoint(sb, e, handlerService, map, template, version);
                            continue;
                        }

                        if (e.AcceptsMessagePack)
                        {
                            sb.AppendLine("            group." + map + "(" + Literal(template) + ", static async (");
                            if (explicitBindings)
                            {
                                foreach (var property in e.Properties.Where(property => property.IsRoute || property.IsQuery))
                                {
                                    var source = property.IsRoute ? "FromRoute" : "FromQuery";
                                    var bindingName = property.IsRoute ? property.BindingName : property.Name;
                                    sb.AppendLine("                [global::Microsoft.AspNetCore.Mvc." + source + "(Name = " + Literal(bindingName) + ")] " + property.TypeFullName + " " + property.Name + ",");
                                }
                            }
                            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
                            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
                            sb.AppendLine("            {");
                            sb.AppendLine("                var body = await global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ReadRequestAsync<" + e.TypeFullName + ">(httpContext, cancellationToken).ConfigureAwait(false);");
                            sb.AppendLine("                if (body is null)");
                            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.BadRequest();");
                            if (explicitBindings)
                            {
                                var assignments = string.Join(", ", e.Properties
                                    .Where(property => property.IsRoute || property.IsQuery)
                                    .Select(property => property.Name + " = " + property.Name));
                                sb.AppendLine("                var request = body with { " + assignments + " };");
                            }
                            else
                            {
                                sb.AppendLine("                var request = body;");
                            }
                            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                            sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
                            sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
                            sb.AppendLine("                return global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.WriteResponse(httpContext, result, cancellationToken);");
                            sb.AppendLine("            }).Accepts<" + e.TypeFullName + ">(\"application/json\", \"application/x-msgpack\").Produces<" + e.Response + ">(200, \"application/json\", \"application/x-msgpack\").WithGroupName(" + Literal("v" + version) + ")" + AuthorizationMetadata(e) + ";");
                            continue;
                        }
                        sb.AppendLine("            group." + map + "(" + Literal(template) + ", static async (");
                        if (explicitBindings)
                        {
                            foreach (var property in e.Properties.Where(property => property.IsRoute || property.IsQuery))
                            {
                                var source = property.IsRoute ? "FromRoute" : "FromQuery";
                                var bindingName = property.IsRoute ? property.BindingName : property.Name;
                                sb.AppendLine("                [global::Microsoft.AspNetCore.Mvc." + source + "(Name = " + Literal(bindingName) + ")] " + property.TypeFullName + " " + property.Name + ",");
                            }

                            sb.AppendLine("                " + e.TypeFullName + " body,");
                        }
                        else
                        {
                            sb.AppendLine("                " + bind + e.TypeFullName + " request,");
                        }
                        sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
                        sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
                        sb.AppendLine("            {");
                        if (explicitBindings)
                        {
                            var assignments = string.Join(", ", e.Properties
                                .Where(property => property.IsRoute || property.IsQuery)
                                .Select(property => property.Name + " = " + property.Name));
                            sb.AppendLine("                var request = body with { " + assignments + " };");
                        }
                        sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                        sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
                        sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
                        sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.Ok(result);");
                        sb.AppendLine("            }).WithGroupName(" + Literal("v" + version) + ")" + AuthorizationMetadata(e) + ";");
                    }
                }
            }

            sb.AppendLine("            configure?.Invoke(group);");
            sb.AppendLine("            return group;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.MinimalApi.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void EmitMultipartEndpoint(
            StringBuilder sb,
            EndpointModel endpoint,
            string handlerService,
            string map,
            string template,
            int version)
        {
            var attachment = endpoint.Properties.Single(property =>
                property.TypeFullName == "global::Ark.MediatorFramework.IArkAttachment");
            var bindings = endpoint.Properties.Where(property => property.IsRoute || property.IsQuery).ToArray();
            sb.Append("            group.").Append(map).Append("(").Append(Literal(template)).AppendLine(", static async (");
            foreach (var property in bindings)
            {
                var source = property.IsRoute ? "FromRoute" : "FromQuery";
                var bindingName = property.IsRoute ? property.BindingName : property.Name;
                sb.Append("                [global::Microsoft.AspNetCore.Mvc.").Append(source)
                    .Append("(Name = ").Append(Literal(bindingName)).Append(")] ")
                    .Append(property.TypeFullName).Append(' ').Append(property.Name).AppendLine(",");
            }

            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                var form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);");
            sb.AppendLine("                if (form.Files.Count != 1)");
            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.BadRequest(\"Exactly one file is required.\");");
            sb.AppendLine("                var file = form.Files[0];");
            sb.AppendLine("                var request = new " + endpoint.TypeFullName + " {");
            foreach (var property in bindings)
                sb.Append("                    ").Append(property.Name).Append(" = ").Append(property.Name).AppendLine(",");
            sb.AppendLine("                    " + attachment.Name + " = new global::Ark.MediatorFramework.ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream),");
            sb.AppendLine("                };");
            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
            sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
            sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.TypedResults.Ok(result);");
            sb.Append("            }).Accepts<global::Microsoft.AspNetCore.Http.IFormFile>(\"multipart/form-data\").WithGroupName(")
                .Append(Literal("v" + version)).Append(')').Append(AuthorizationMetadata(endpoint)).AppendLine(";");
        }

        private static string AuthorizationMetadata(EndpointModel endpoint)
        {
            if (endpoint.AllowAnonymous)
                return ".AllowAnonymous()";

            return string.IsNullOrWhiteSpace(endpoint.Policy)
                ? ".RequireAuthorization()"
                : ".RequireAuthorization(" + Literal(endpoint.Policy!) + ")";
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
            public EndpointModel(
                string typeFullName,
                string typeName,
                string verb,
                string template,
                string response,
                HandlerKind kind,
                int httpIntroducedIn,
                int httpRetiredIn,
                bool acceptsMessagePack,
                string? policy,
                bool allowAnonymous,
                ImmutableArray<PropertyModel> properties,
                int attachmentCount,
                Location? location)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Verb = verb;
                Template = template;
                Response = response;
                Kind = kind;
                HttpIntroducedIn = httpIntroducedIn;
                HttpRetiredIn = httpRetiredIn;
                AcceptsMessagePack = acceptsMessagePack;
                Policy = policy;
                AllowAnonymous = allowAnonymous;
                Properties = properties;
                AttachmentCount = attachmentCount;
                Location = location;
            }

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string Verb { get; }
            public string Template { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public int HttpIntroducedIn { get; }
            public int HttpRetiredIn { get; }
            public bool AcceptsMessagePack { get; }
            public string? Policy { get; }
            public bool AllowAnonymous { get; }
            public ImmutableArray<PropertyModel> Properties { get; }
            public int AttachmentCount { get; }
            public Location? Location { get; }
        }

        private readonly struct PropertyModel
        {
            public PropertyModel(string name, string typeFullName, bool isRoute, string bindingName, bool isQuery)
            {
                Name = name;
                TypeFullName = typeFullName;
                IsRoute = isRoute;
                BindingName = bindingName;
                IsQuery = isQuery;
            }

            public string Name { get; }
            public string TypeFullName { get; }
            public bool IsRoute { get; }
            public string BindingName { get; }
            public bool IsQuery { get; }
        }
    }
}
