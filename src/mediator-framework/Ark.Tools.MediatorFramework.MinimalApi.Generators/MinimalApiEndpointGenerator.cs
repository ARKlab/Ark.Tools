// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        private const string ServerSetAttribute = "Ark.MediatorFramework.ServerSetAttribute";
        private const string RebusMessageAttribute = "Ark.MediatorFramework.RebusMessageAttribute";
        private const string ArkAttachment = "Ark.MediatorFramework.IArkAttachment";
        private static readonly DiagnosticDescriptor MultipleAttachments = new DiagnosticDescriptor(
            "ARKMF001",
            "Only one attachment is supported",
            "HTTP endpoint '{0}' declares more than one IArkAttachment property",
            "Ark.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor ServerSetPropertyCannotBeReset = new DiagnosticDescriptor(
            "ARKMF002",
            "Server-set property cannot be reset",
            "HTTP endpoint '{0}' has server-set property '{1}' without an accessible setter",
            "Ark.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor PossibleMassAssignment = new DiagnosticDescriptor(
            "ARKMF003",
            "Possible mass assignment",
            "HTTP endpoint '{0}' has property '{1}' that may be server-owned; mark it with [ServerSet] or suppress this warning",
            "Ark.MediatorFramework",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpointAssemblies = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is InvocationExpressionSyntax invocation
                        && invocation.Expression.ToString().Contains("MapArkEndpoints", StringComparison.Ordinal),
                    static (syntaxContext, _) => GetAssemblyName(syntaxContext, "MapArkEndpoints"))
                .Where(static assemblyName => assemblyName is not null)
                .Select(static (assemblyName, _) => assemblyName!)
                .Collect();
            var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                    HttpEndpointAttribute,
                    static (_, _) => true,
                    static (attributeContext, _) => ExtractSourceEndpoint(attributeContext))
                .Where(static endpoint => endpoint is not null)
                .Select(static (endpoint, _) => endpoint!.Value);
            var referencedEndpoints = context.CompilationProvider
                .Combine(endpointAssemblies)
                .SelectMany(static (pair, _) => GetReferencedEndpoints(pair.Left, pair.Right));

            var collected = sourceEndpoints.Collect().Combine(referencedEndpoints.Collect());

            context.RegisterSourceOutput(
                collected,
                static (spc, pair) => Emit(spc, pair.Left.AddRange(pair.Right)));
        }

        private static EndpointModel? ExtractSourceEndpoint(GeneratorAttributeSyntaxContext context)
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;
            var http = context.Attributes[0];
            var compilation = context.SemanticModel.Compilation;
            return Extract(
                type,
                http,
                compilation.GetTypeByMetadataName(BindFromQueryAttribute),
                compilation.GetTypeByMetadataName(ServerSetAttribute),
                compilation.GetTypeByMetadataName(ArkAttachment),
                compilation.GetTypeByMetadataName(RebusMessageAttribute));
        }

        private static string? GetAssemblyName(GeneratorSyntaxContext context, string methodName)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var genericName = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .FirstOrDefault(name => name.Identifier.ValueText == methodName);
            if (genericName is null || genericName.TypeArgumentList.Arguments.Count != 1)
                return null;

            return context.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type?.ContainingAssembly?.Name;
        }

        private static ImmutableArray<EndpointModel> GetReferencedEndpoints(
            Compilation compilation,
            ImmutableArray<string> endpointAssemblies)
        {
            var httpAttr = compilation.GetTypeByMetadataName(HttpEndpointAttribute);
            if (httpAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = httpAttr.ContainingAssembly;
            var bindFromQueryAttr = compilation.GetTypeByMetadataName(BindFromQueryAttribute);
            var serverSetAttr = compilation.GetTypeByMetadataName(ServerSetAttribute);
            var rebusMessageAttr = compilation.GetTypeByMetadataName(RebusMessageAttribute);
            var attachmentType = compilation.GetTypeByMetadataName(ArkAttachment);
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _referencedAssemblies(compilation, runtimeAssembly)
                .Where(assembly => endpointAssemblies.Contains(assembly.Name, StringComparer.Ordinal)))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var http = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, httpAttr));
                    if (http is null)
                        continue;

                    var model = Extract(type, http, bindFromQueryAttr, serverSetAttr, attachmentType, rebusMessageAttr);
                    if (model is not null)
                        builder.Add(model.Value);
                }
            }

            return builder.ToImmutable();
        }

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
                }
            }
        }

        private static EndpointModel Extract(
            INamedTypeSymbol type,
            AttributeData http,
            INamedTypeSymbol? bindFromQueryAttr,
            INamedTypeSymbol? serverSetAttr,
            INamedTypeSymbol? attachmentType,
            INamedTypeSymbol? rebusMessageAttr)
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

                if (def == "global::Ark.Tools.Solid.ICommand")
                {
                    kind = HandlerKind.Command;
                    break;
                }
            }

            var diagnostics = new List<DiagnosticInfo>();
            if (kind == HandlerKind.None || (kind != HandlerKind.Command && response is null))
            {
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.UnsupportedHandlerKind, type.Name, GetLocation(http)));
                return EndpointModel.Invalid(type, diagnostics);
            }

            if (http.ConstructorArguments.Length != 2)
                return EndpointModel.Invalid(type, diagnostics);

            var verb = http.ConstructorArguments[0].Value as string;
            var template = http.ConstructorArguments[1].Value as string;
            if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(template))
                return EndpointModel.Invalid(type, diagnostics);

            verb = verb!.ToUpperInvariant();
            if (verb is not ("GET" or "POST" or "PUT" or "DELETE" or "PATCH"))
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.UnknownHttpVerb, type.Name, GetLocation(http), verb));
            var httpIntroducedIn = NamedInt(http, "IntroducedIn", 1);
            var httpRetiredIn = NamedInt(http, "RetiredIn", 0);
            var successStatusCode = NamedInt(http, "SuccessStatusCode", 0);
            var nullResultStatusCode = NamedInt(http, "NullResultStatusCode", 0);
            var acceptsMessagePack = NamedBool(http, "AcceptsMessagePack");
            var policy = NamedString(http, "Policy");
            var allowAnonymous = NamedBool(http, "AllowAnonymous");
            var requireAntiforgery = NamedBool(http, "RequireAntiforgery");
            var maxRequestBodySizeBytes = NamedLong(http, "MaxRequestBodySizeBytes");
            var allowedContentTypes = NamedStringArray(http, "AllowedContentTypes");
            var ownerQueue = rebusMessageAttr is null
                ? null
                : type.GetAttributes()
                    .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, rebusMessageAttr))
                    .Select(attribute => NamedString(attribute, "OwnerQueue"))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
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
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, bindFromQueryAttr)),
                    serverSetAttr is not null && property.GetAttributes().Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serverSetAttr)),
                    property.SetMethod is not null && property.SetMethod.DeclaredAccessibility == Accessibility.Public))
                .ToImmutableArray();
            foreach (var routeName in routeNames)
            {
                if (!properties.Any(property => string.Equals(property.Name, routeName, StringComparison.OrdinalIgnoreCase)))
                    diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MissingRouteProperty, type.Name, GetLocation(http), routeName));
            }
            var bodyBinding = verb is not ("GET" or "DELETE");
            if (bodyBinding && (!type.IsRecord || properties.Any(property => !property.HasPublicSetter)))
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidContractShape, type.Name, GetLocation(http)));
            var attachmentProperties = attachmentType is null
                ? ImmutableArray<PropertyModel>.Empty
                : properties.Where(property => property.TypeFullName == "global::Ark.MediatorFramework.IArkAttachment").ToImmutableArray();

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                verb,
                template!,
                response ?? "global::System.Void",
                kind,
                httpIntroducedIn,
                httpRetiredIn,
                successStatusCode,
                nullResultStatusCode,
                acceptsMessagePack,
                policy,
                allowAnonymous,
                requireAntiforgery,
                maxRequestBodySizeBytes,
                allowedContentTypes,
                ownerQueue,
                properties,
                type.IsRecord,
                properties.Where(property => property.IsServerSet && !property.HasPublicSetter)
                    .Select(property => property.Name)
                    .ToImmutableArray(),
                properties.Where(property => !property.IsServerSet
                    && property.Name is "TenantId" or "UserId" or "IsAdmin" or "Role" or "Roles")
                    .Select(property => property.Name)
                    .ToImmutableArray(),
                attachmentProperties.Length,
                type.Locations.FirstOrDefault(),
                diagnostics);
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

        private static long NamedLong(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is long value ? value : 0;
        }

        private static ImmutableArray<string> NamedStringArray(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            if (argument.Value.Kind != TypedConstantKind.Array)
                return ImmutableArray<string>.Empty;

            return argument.Value.Values
                .Where(value => value.Value is string)
                .Select(value => (string)value.Value!)
                .ToImmutableArray();
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
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.RouteGroupBuilder MapArkEndpoints<TAssemblyMarker>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, global::System.Action<global::Microsoft.AspNetCore.Routing.RouteGroupBuilder>? configure = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            var group = endpoints.MapGroup(string.Empty);");

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
                    foreach (var diagnostic in e.Diagnostics)
                        spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
                    if (!e.IsValid)
                        continue;
                    foreach (var property in e.InvalidServerSetProperties)
                        spc.ReportDiagnostic(Diagnostic.Create(ServerSetPropertyCannotBeReset, e.Location, e.TypeName, property));
                    foreach (var property in e.SuspiciousProperties)
                        spc.ReportDiagnostic(Diagnostic.Create(PossibleMassAssignment, e.Location, e.TypeName, property));

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
                        if (e.Kind == HandlerKind.Command)
                        {
                            EmitCommandEndpoint(sb, e, map, template, version);
                            continue;
                        }
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
                                foreach (var property in e.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet))
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
                                    .Select(property => property.IsServerSet ? property.Name + " = default" : property.Name + " = " + property.Name)
                                    .Concat(e.ServerSetProperties.Where(property => !e.Properties.Any(candidate => candidate.Name == property))
                                        .Select(property => property + " = default")));
                                sb.AppendLine("                var request = body with { " + assignments + " };");
                            }
                            else
                            {
                                if (e.IsRecord && e.ServerSetProperties.Length > 0)
                                    sb.AppendLine("                var request = body with { " + string.Join(", ", e.ServerSetProperties.Select(property => property + " = default")) + " };");
                                else
                                    sb.AppendLine("                var request = body;");
                            }
                            EmitServerSetAssignments(sb, e, "request");
                            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                            sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
                            sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
                            sb.AppendLine("                return global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.WriteResponse(httpContext, result, cancellationToken, "
                                + SuccessStatusCode(e) + ", " + NullResultStatusCode(e) + ");");
                            sb.AppendLine("            }).Accepts<" + e.TypeFullName + ">(\"application/json\", \"application/x-msgpack\").Produces<" + e.Response + ">("
                                + SuccessStatusCode(e) + ", \"application/json\", \"application/x-msgpack\").Produces(" + NullResultStatusCode(e)
                                + ").WithGroupName(" + Literal("v" + version) + ")" + AuthorizationMetadata(e) + ";");
                            continue;
                        }
                        sb.AppendLine("            group." + map + "(" + Literal(template) + ", static async (");
                        if (explicitBindings)
                        {
                            foreach (var property in e.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet))
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
                                .Select(property => property.Name + " = " + property.Name)
                                .Concat(e.ServerSetProperties.Select(property => property + " = default")));
                            sb.AppendLine("                var request = body with { " + assignments + " };");
                        }
                        else if (e.IsRecord && e.ServerSetProperties.Length > 0)
                        {
                            sb.AppendLine("                request = request with { " + string.Join(", ", e.ServerSetProperties.Select(property => property + " = default")) + " };");
                        }
                        EmitServerSetAssignments(sb, e, "request");
                        sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                        sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
                        sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
                        sb.AppendLine("                if (result is null)");
                        sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)" + NullResult(e) + ";");
                        sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)" + SuccessResult(e) + ";");
                        sb.AppendLine("            }).Produces<" + e.Response + ">(" + SuccessStatusCode(e) + ").Produces(" + NullResultStatusCode(e)
                            + ").WithGroupName(" + Literal("v" + version) + ")" + AuthorizationMetadata(e) + ";");
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

        private static void EmitServerSetAssignments(StringBuilder sb, EndpointModel endpoint, string variable)
        {
            if (endpoint.IsRecord)
                return;

            foreach (var property in endpoint.ServerSetProperties)
                sb.Append("                ").Append(variable).Append('.').Append(property).AppendLine(" = default;");
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
            var bindings = endpoint.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet).ToArray();
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
            if (!endpoint.AllowedContentTypes.IsDefaultOrEmpty)
            {
                var allowedTypes = string.Join(", ", endpoint.AllowedContentTypes.Select(Literal));
                sb.AppendLine("                if (!global::System.Linq.Enumerable.Contains(new[] { "
                    + allowedTypes
                    + " }, file.ContentType, global::System.StringComparer.OrdinalIgnoreCase))");
                sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.StatusCode(415);");
            }
            sb.AppendLine("                var request = new " + endpoint.TypeFullName + " {");
            foreach (var property in bindings)
                sb.Append("                    ").Append(property.Name).Append(" = ").Append(property.Name).AppendLine(",");
            sb.AppendLine("                    " + attachment.Name + " = new global::Ark.MediatorFramework.ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream),");
            sb.AppendLine("                };");
            EmitServerSetAssignments(sb, endpoint, "request");
            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
            sb.AppendLine("                var handler = container.GetInstance<" + handlerService + ">();");
            sb.AppendLine("                var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine("                if (result is null)");
            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)" + NullResult(endpoint) + ";");
            sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)" + SuccessResult(endpoint) + ";");
            sb.Append("            }).Accepts<global::Microsoft.AspNetCore.Http.IFormFile>(\"multipart/form-data\").WithGroupName(")
                .Append(Literal("v" + version)).Append(')').Append(MultipartMetadata(endpoint))
                .Append(".Produces<").Append(endpoint.Response).Append(">(").Append(SuccessStatusCode(endpoint))
                .Append(").Produces(").Append(NullResultStatusCode(endpoint)).Append(')')
                .Append(AuthorizationMetadata(endpoint)).AppendLine(";");
        }

        private static void EmitCommandEndpoint(
            StringBuilder sb,
            EndpointModel endpoint,
            string map,
            string template,
            int version)
        {
            var bodyVerb = endpoint.Verb != "GET" && endpoint.Verb != "DELETE";
            var explicitBindings = bodyVerb && endpoint.Properties.Any(property => property.IsRoute || property.IsQuery);
            sb.Append("            group.").Append(map).Append("(").Append(Literal(template)).AppendLine(", static async (");
            if (explicitBindings)
            {
                foreach (var property in endpoint.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet))
                {
                    var source = property.IsRoute ? "FromRoute" : "FromQuery";
                    var bindingName = property.IsRoute ? property.BindingName : property.Name;
                    sb.Append("                [global::Microsoft.AspNetCore.Mvc.").Append(source)
                        .Append("(Name = ").Append(Literal(bindingName)).Append(")] ")
                        .Append(property.TypeFullName).Append(' ').Append(property.Name).AppendLine(",");
                }

                sb.AppendLine("                " + endpoint.TypeFullName + " body,");
            }
            else
            {
                sb.AppendLine("                " + (bodyVerb ? string.Empty : "[global::Microsoft.AspNetCore.Http.AsParameters] ") + endpoint.TypeFullName + " request,");
            }

            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine("            {");
            if (explicitBindings)
            {
                var assignments = string.Join(", ", endpoint.Properties
                    .Where(property => property.IsRoute || property.IsQuery)
                    .Select(property => property.Name + " = " + property.Name)
                    .Concat(endpoint.ServerSetProperties.Select(property => property + " = default")));
                sb.AppendLine("                var request = body with { " + assignments + " };");
            }
            else if (endpoint.IsRecord && endpoint.ServerSetProperties.Length > 0)
            {
                sb.AppendLine("                request = request with { " + string.Join(", ", endpoint.ServerSetProperties.Select(property => property + " = default")) + " };");
            }
            EmitServerSetAssignments(sb, endpoint, "request");
            if (endpoint.OwnerQueue is not null)
            {
                sb.AppendLine("                var bus = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Rebus.IBus>(httpContext.RequestServices);");
                sb.AppendLine("                await bus.Advanced.Routing.Send(" + Literal(endpoint.OwnerQueue) + ", request).ConfigureAwait(false);");
                sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.Accepted();");
            }
            else
            {
                sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                sb.AppendLine("                var handler = container.GetInstance<global::Ark.Tools.Solid.ICommandHandler<" + endpoint.TypeFullName + ">>();");
                sb.AppendLine("                await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.NoContent();");
            }
            sb.Append("            }).WithGroupName(").Append(Literal("v" + version)).Append(')');
            sb.Append(endpoint.OwnerQueue is null ? ".Produces(204)" : ".Produces(202)");
            sb.Append(AuthorizationMetadata(endpoint)).AppendLine(";");
        }

        private static string MultipartMetadata(EndpointModel endpoint)
        {
            var metadata = new StringBuilder();
            if (!endpoint.RequireAntiforgery)
                metadata.Append(" /* Bearer-token API upload: antiforgery validation is intentionally disabled. */.DisableAntiforgery()");
            if (endpoint.MaxRequestBodySizeBytes > 0)
                metadata.Append(".WithMetadata(new global::Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(")
                    .Append(endpoint.MaxRequestBodySizeBytes)
                    .Append("L))");
            return metadata.ToString();
        }

        private static int SuccessStatusCode(EndpointModel endpoint)
            => endpoint.SuccessStatusCode == 0 ? 200 : endpoint.SuccessStatusCode;

        private static int NullResultStatusCode(EndpointModel endpoint)
            => endpoint.NullResultStatusCode == 0
                ? endpoint.Kind == HandlerKind.Query ? 404 : 204
                : endpoint.NullResultStatusCode;

        private static string NullResult(EndpointModel endpoint)
            => NullResultStatusCode(endpoint) switch
            {
                200 => "global::Microsoft.AspNetCore.Http.TypedResults.Ok()",
                204 => "global::Microsoft.AspNetCore.Http.TypedResults.NoContent()",
                404 => "global::Microsoft.AspNetCore.Http.TypedResults.NotFound()",
                var statusCode => "global::Microsoft.AspNetCore.Http.Results.StatusCode(" + statusCode + ")",
            };

        private static string SuccessResult(EndpointModel endpoint)
            => SuccessStatusCode(endpoint) == 200
                ? "global::Microsoft.AspNetCore.Http.TypedResults.Ok(result)"
                : "global::Microsoft.AspNetCore.Http.Results.Json(result, statusCode: " + SuccessStatusCode(endpoint) + ")";

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
            _ => throw new InvalidOperationException("Unknown HTTP verb should have been diagnosed before emission"),
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
            Command = 3,
        }

        private readonly record struct EndpointModel
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
                int successStatusCode,
                int nullResultStatusCode,
                bool acceptsMessagePack,
                string? policy,
                bool allowAnonymous,
                bool requireAntiforgery,
                long maxRequestBodySizeBytes,
                ImmutableArray<string> allowedContentTypes,
                string? ownerQueue,
                ImmutableArray<PropertyModel> properties,
                bool isRecord,
                ImmutableArray<string> invalidServerSetProperties,
                ImmutableArray<string> suspiciousProperties,
                int attachmentCount,
                Location? location,
                IReadOnlyList<DiagnosticInfo> diagnostics)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Verb = verb;
                Template = template;
                Response = response;
                Kind = kind;
                HttpIntroducedIn = httpIntroducedIn;
                HttpRetiredIn = httpRetiredIn;
                SuccessStatusCode = successStatusCode;
                NullResultStatusCode = nullResultStatusCode;
                AcceptsMessagePack = acceptsMessagePack;
                Policy = policy;
                AllowAnonymous = allowAnonymous;
                RequireAntiforgery = requireAntiforgery;
                MaxRequestBodySizeBytes = maxRequestBodySizeBytes;
                AllowedContentTypes = allowedContentTypes;
                OwnerQueue = ownerQueue;
                Properties = properties;
                IsRecord = isRecord;
                ServerSetProperties = properties.Where(property => property.IsServerSet).Select(property => property.Name).ToImmutableArray();
                InvalidServerSetProperties = invalidServerSetProperties;
                SuspiciousProperties = suspiciousProperties;
                AttachmentCount = attachmentCount;
                Location = location;
                Diagnostics = diagnostics;
                IsValid = diagnostics.Count == 0;
            }

            private EndpointModel(string typeFullName, string typeName, IReadOnlyList<DiagnosticInfo> diagnostics)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Diagnostics = diagnostics;
                IsValid = false;
                Verb = string.Empty;
                Template = string.Empty;
                Response = string.Empty;
                Kind = HandlerKind.None;
                AllowedContentTypes = ImmutableArray<string>.Empty;
                Properties = ImmutableArray<PropertyModel>.Empty;
                ServerSetProperties = ImmutableArray<string>.Empty;
                InvalidServerSetProperties = ImmutableArray<string>.Empty;
                SuspiciousProperties = ImmutableArray<string>.Empty;
            }

            public static EndpointModel Invalid(INamedTypeSymbol type, IReadOnlyList<DiagnosticInfo> diagnostics)
                => new(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), type.Name, diagnostics);

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string Verb { get; }
            public string Template { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public int HttpIntroducedIn { get; }
            public int HttpRetiredIn { get; }
            public int SuccessStatusCode { get; }
            public int NullResultStatusCode { get; }
            public bool AcceptsMessagePack { get; }
            public string? Policy { get; }
            public bool AllowAnonymous { get; }

            public bool RequireAntiforgery { get; }

            public long MaxRequestBodySizeBytes { get; }

            public ImmutableArray<string> AllowedContentTypes { get; }
            public string? OwnerQueue { get; }
            public ImmutableArray<PropertyModel> Properties { get; }
            public bool IsRecord { get; }
            public ImmutableArray<string> ServerSetProperties { get; }
            public ImmutableArray<string> InvalidServerSetProperties { get; }
            public ImmutableArray<string> SuspiciousProperties { get; }
            public int AttachmentCount { get; }
            public Location? Location { get; }
            public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }
            public bool IsValid { get; }
        }

        private readonly record struct DiagnosticInfo
        {
            public DiagnosticInfo(DiagnosticDescriptor descriptor, string typeName, Location location, params object[] arguments)
            {
                Descriptor = descriptor;
                Location = location;
                Arguments = arguments.Length == 0 ? new object[] { typeName } : new[] { (object)typeName }.Concat(arguments).ToArray();
            }

            public DiagnosticDescriptor Descriptor { get; }
            public Location Location { get; }
            public object[] Arguments { get; }
        }

        private static Location GetLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        private readonly record struct PropertyModel
        {
            public PropertyModel(string name, string typeFullName, bool isRoute, string bindingName, bool isQuery, bool isServerSet, bool hasPublicSetter)
            {
                Name = name;
                TypeFullName = typeFullName;
                IsRoute = isRoute;
                BindingName = bindingName;
                IsQuery = isQuery;
                IsServerSet = isServerSet;
                HasPublicSetter = hasPublicSetter;
            }

            public string Name { get; }
            public string TypeFullName { get; }
            public bool IsRoute { get; }
            public string BindingName { get; }
            public bool IsQuery { get; }
            public bool IsServerSet { get; }
            public bool HasPublicSetter { get; }
        }
    }
}
