// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.AzureFunctions.Generators;

/// <summary>Generates isolated-worker HTTP triggers for selected mediator contracts.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class AzureFunctionsEndpointGenerator : IIncrementalGenerator
{
    // ponytail: [GeneratedRegex] is not available for netstandard2.0 targets; static field compiles and caches once.
    private static readonly Regex _routeParamRegex = new Regex(@"\{(?<param>[^}:]+)(?::[^}]+)?\}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private const string HostAttribute = "Ark.MediatorFramework.HttpHostAttribute";
    private const string EndpointAttribute = "Ark.MediatorFramework.HttpEndpointAttribute";
    private const string VersioningAttribute = "Ark.MediatorFramework.VersioningAttribute";
    private const string HttpRouteAttribute = "Ark.MediatorFramework.HttpRouteAttribute";
    private const string HttpQueryAttribute = "Ark.MediatorFramework.HttpQueryAttribute";
    private const string ServerSetAttribute = "Ark.MediatorFramework.ServerSetAttribute";
    private const string ETagAttribute = "Ark.MediatorFramework.ETagAttribute";
    private const string SolidRequest = "global::Ark.Tools.Solid.IRequest<TResponse>";
    private const string SolidQuery = "global::Ark.Tools.Solid.IQuery<TResult>";
    private const string SolidCommand = "global::Ark.Tools.Solid.ICommand";

    private static readonly DiagnosticDescriptor MessagePackNotSupported = new(
        "ARKMF030",
        "MessagePack is not supported by Azure Functions",
        "HTTP endpoint '{0}' enables MessagePack and cannot be selected by an Azure Functions host",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRoute = new(
        "ARKMF031",
        "Duplicate Azure Functions route",
        "HTTP endpoints '{0}' and '{1}' resolve to the same Azure Functions route '{2}'",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateFunction = new(
        "ARKMF032",
        "Duplicate Azure Functions name",
        "HTTP endpoints '{0}' and '{1}' resolve to the same Azure Functions name '{2}'",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hosts = context.SyntaxProvider.ForAttributeWithMetadataName(
                HostAttribute,
                static (_, _) => true,
                static (attributeContext, _) => ExtractHost(attributeContext))
            .Where(static host => host is not null)
            .Select(static (host, _) => host!.Value)
            .Collect();
        var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                EndpointAttribute,
                static (_, _) => true,
                static (attributeContext, _) => new EndpointCandidate(
                    (INamedTypeSymbol)attributeContext.TargetSymbol,
                    attributeContext.Attributes[0]))
            .Collect();

        context.RegisterSourceOutput(
            hosts.Combine(sourceEndpoints),
            static (productionContext, pair) => Emit(productionContext, pair.Left, pair.Right));
    }

    private static HostInfo? ExtractHost(GeneratorAttributeSyntaxContext context)
    {
        var host = context.Attributes[0];
        if (host.ConstructorArguments.Length < 2
            || host.ConstructorArguments[0].Value is not INamedTypeSymbol marker
            || host.ConstructorArguments[1].Value is not string prefix)
            return null;

        return new HostInfo(
            marker,
            prefix,
            GetTypes(host, "IncludedContracts"),
            GetTypes(host, "ExcludedContracts"),
            marker.Locations.Any(location => location.IsInSource));
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<HostInfo> hosts,
        ImmutableArray<EndpointCandidate> sourceEndpoints)
    {
        if (hosts.IsDefaultOrEmpty)
            return;

        var endpoints = new List<Endpoint>();
        foreach (var host in hosts)
        {
            var candidates = host.MarkerIsInSource
                ? sourceEndpoints.Where(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate.Type.ContainingAssembly, host.Marker.ContainingAssembly))
                : AllTypes(host.Marker.ContainingAssembly.GlobalNamespace)
                    .Select(type => new EndpointCandidate(
                        type,
                        type.GetAttributes().FirstOrDefault(attribute =>
                            attribute.AttributeClass?.ToDisplayString() == EndpointAttribute)))
                    .Where(candidate => candidate.Attribute is not null);
            foreach (var candidate in candidates)
            {
                if (!IsSelected(candidate.Type, host.Marker.ContainingAssembly, host.Included, host.Excluded))
                    continue;

                var endpoint = CreateEndpoint(candidate.Type, candidate.Attribute!, host.Prefix);
                if (endpoint is not null)
                    endpoints.Add(endpoint.Value);
            }
        }

        var maxVersion = endpoints.Count == 0
            ? 1
            : endpoints.Max(item => item.Retired > 0 ? item.Retired - 1 : item.Introduced);
        var expanded = endpoints.SelectMany(endpoint =>
            Enumerable.Range(endpoint.Introduced, Math.Max(1, maxVersion - endpoint.Introduced + 1))
                .Where(version => endpoint.Retired == 0 || version < endpoint.Retired)
                .Select(version => endpoint with
                {
                    Route = ExpandRoute(endpoint.Prefix, endpoint.Template, version),
                    FunctionName = Sanitize(endpoint.TypeName + "_v" + version.ToString(CultureInfo.InvariantCulture)),
                }));

        var valid = new List<Endpoint>();
        foreach (var endpoint in expanded.OrderBy(item => item.FunctionName, StringComparer.Ordinal))
        {
            if (endpoint.MessagePack)
            {
                context.ReportDiagnostic(Diagnostic.Create(MessagePackNotSupported, endpoint.Location, endpoint.TypeName));
                continue;
            }

            var duplicateRoute = valid.FirstOrDefault(item =>
                string.Equals(item.Verb, endpoint.Verb, StringComparison.Ordinal)
                && string.Equals(item.Route, endpoint.Route, StringComparison.Ordinal));
            if (duplicateRoute.TypeName is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateRoute, endpoint.Location, duplicateRoute.TypeName, endpoint.TypeName, endpoint.Route));
                continue;
            }

            var duplicateFunction = valid.FirstOrDefault(item =>
                string.Equals(item.FunctionName, endpoint.FunctionName, StringComparison.Ordinal));
            if (duplicateFunction.TypeName is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateFunction, endpoint.Location, duplicateFunction.TypeName, endpoint.TypeName, endpoint.FunctionName));
                continue;
            }

            valid.Add(endpoint);
        }

        if (valid.Count == 0)
            return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("namespace Ark.MediatorFramework.AzureFunctions.Generated;");
        source.AppendLine();
        source.AppendLine("public static class ArkGeneratedFunctions");
        source.AppendLine("{");
        foreach (var endpoint in valid)
        {
            EmitFunction(source, endpoint);
        }
        source.AppendLine("}");
        context.AddSource("ArkGeneratedFunctions.g.cs", source.ToString());
    }

    private static void EmitFunction(StringBuilder source, Endpoint endpoint)
    {
        var hasBody = endpoint.Verb is "POST" or "PUT" or "PATCH";
        var routeProperties = endpoint.Properties.Where(p => p.IsRoute && !p.IsServerSet).ToArray();
        var queryProperties = endpoint.Properties.Where(p => p.IsQuery && !p.IsServerSet).ToArray();
        var serverSetProperties = endpoint.Properties.Where(p => p.IsServerSet).ToArray();

        source.Append("    [global::Microsoft.Azure.Functions.Worker.Function(\"")
            .Append(endpoint.FunctionName).AppendLine("\")]");
        source.Append("    public static async global::System.Threading.Tasks.Task<global::Microsoft.AspNetCore.Http.IResult> ")
            .Append(endpoint.FunctionName).AppendLine("(");
        source.Append("        [global::Microsoft.Azure.Functions.Worker.HttpTrigger(")
            .Append("global::Microsoft.Azure.Functions.Worker.AuthorizationLevel.Anonymous, \"")
            .Append(endpoint.Verb.ToLowerInvariant()).Append("\", Route = \"")
            .Append(Escape(endpoint.Route)).AppendLine("\")]");
        source.AppendLine("        global::Microsoft.AspNetCore.Http.HttpRequest request,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
        source.AppendLine("    {");
        source.Append("        var _authentication = await global::Ark.MediatorFramework.AzureFunctions.ArkAzureFunctionsInvocation.AuthenticateAsync(request.HttpContext, ")
            .Append(endpoint.AllowAnonymous ? "true" : "false")
            .AppendLine(").ConfigureAwait(false);");
        source.AppendLine("        if (_authentication is not null)");
        source.AppendLine("            return _authentication;");

        // Body or default-instance binding
        if (hasBody)
        {
            source.Append("        ").Append(endpoint.FullyQualifiedType).AppendLine("? _bodyNullable;");
            source.AppendLine("        try");
            source.AppendLine("        {");
            source.Append("            _bodyNullable = await request.ReadFromJsonAsync<").Append(endpoint.FullyQualifiedType).AppendLine(">(cancellationToken).ConfigureAwait(false);");
            source.AppendLine("        }");
            source.AppendLine("        catch (global::System.Text.Json.JsonException ex)");
            source.AppendLine("        {");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: ex.Message);");
            source.AppendLine("        }");
            source.AppendLine("        if (_bodyNullable is null)");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: \"Request body is missing or could not be deserialized.\");");
            source.AppendLine("        var body = _bodyNullable;");
        }
        else
        {
            source.Append("        var body = new ").Append(endpoint.FullyQualifiedType).AppendLine("();");
        }

        // Route value binding (per-property, no runtime reflection)
        foreach (var prop in routeProperties)
        {
            if (prop.IsString)
            {
                source.Append("        body.").Append(prop.Name).Append(" = request.RouteValues[").Append(Literal(prop.BindingName)).AppendLine("]?.ToString();");
            }
            else
            {
                var varName = "_route_" + prop.Name;
                source.Append("        if (!global::Ark.Tools.Core.ArkTypeConverter.TryConvertSafe<").Append(prop.TypeFullName).Append(">(request.RouteValues[").Append(Literal(prop.BindingName)).Append("]?.ToString(), out var ").Append(varName).AppendLine("))");
                source.Append("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"BINDING_FAILURE\", detail: \"Route value '").Append(prop.BindingName).Append("' could not be bound to type '").Append(prop.TypeFullName).AppendLine("'.\");");
                source.Append("        body.").Append(prop.Name).Append(" = ").Append(varName).AppendLine(";");
            }
        }

        // Query string binding (per-property, no runtime reflection)
        foreach (var prop in queryProperties)
        {
            source.Append("        if (request.Query.TryGetValue(").Append(Literal(prop.Name)).Append(", out var _qs_").Append(prop.Name).AppendLine("))");
            source.AppendLine("        {");
            if (prop.IsString)
            {
                source.Append("            body.").Append(prop.Name).Append(" = (string?)_qs_").Append(prop.Name).AppendLine(";");
            }
            else
            {
                var varName = "_query_" + prop.Name;
                source.Append("            if (!global::Ark.Tools.Core.ArkTypeConverter.TryConvertSafe<").Append(prop.TypeFullName).Append(">(_qs_").Append(prop.Name).Append(", out var ").Append(varName).AppendLine("))");
                source.Append("                return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"BINDING_FAILURE\", detail: \"Query value '").Append(prop.Name).Append("' could not be bound to type '").Append(prop.TypeFullName).AppendLine("'.\");");
                source.Append("            body.").Append(prop.Name).Append(" = ").Append(varName).AppendLine(";");
            }
            source.AppendLine("        }");
        }

        // Server-set property reset (per-property, no runtime reflection)
        foreach (var prop in serverSetProperties)
        {
            source.Append("        body.").Append(prop.Name).AppendLine(" = default;");
        }
        var _etagProperties = endpoint.Properties.Where(p => p.IsETag).ToArray();
        if (_etagProperties.Length > 0)
        {
            source.AppendLine("        var _etag = global::Ark.MediatorFramework.AzureFunctions.ArkAzureFunctionsResults.ReadPrecondition(request.HttpContext);");
            foreach (var prop in _etagProperties)
                source.Append("        if (_etag is not null) body.").Append(prop.Name).AppendLine(" = _etag;");
        }

        // Dispatch via Simple Injector scope
        source.AppendLine("        try");
        source.AppendLine("        {");
        source.AppendLine("        var _container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(request.HttpContext.RequestServices);");
        source.AppendLine("        await using var _scope = global::SimpleInjector.Lifestyles.AsyncScopedLifestyle.BeginScope(_container);");

        if (endpoint.Kind == HandlerKind.Command)
        {
            source.Append("        var _handler = _container.GetInstance<global::Ark.Tools.Solid.ICommandHandler<").Append(endpoint.FullyQualifiedType).AppendLine(">>();");
            source.AppendLine("        await _handler.ExecuteAsync(body, cancellationToken).ConfigureAwait(false);");
            source.Append("        return global::Microsoft.AspNetCore.Http.Results.StatusCode(")
                .Append(endpoint.SuccessStatusCode == 200 ? "204" : endpoint.SuccessStatusCode.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
        }
        else if (endpoint.Kind == HandlerKind.Query)
        {
            source.Append("        var _handler = _container.GetInstance<global::Ark.Tools.Solid.IQueryHandler<").Append(endpoint.FullyQualifiedType).Append(", ").Append(endpoint.ResponseType).AppendLine(">>();");
            source.AppendLine("        var _result = await _handler.ExecuteAsync(body, cancellationToken).ConfigureAwait(false);");
            source.Append("        if (_result is null) return global::Microsoft.AspNetCore.Http.Results.StatusCode(")
                .Append(endpoint.NullResultStatusCode == 0 ? "404" : endpoint.NullResultStatusCode.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
            EmitResponseETag(source, endpoint, "_result");
            source.Append("        return global::Microsoft.AspNetCore.Http.Results.Json(_result, statusCode: ")
                .Append(endpoint.SuccessStatusCode.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
        }
        else
        {
            source.Append("        var _handler = _container.GetInstance<global::Ark.Tools.Solid.IRequestHandler<").Append(endpoint.FullyQualifiedType).Append(", ").Append(endpoint.ResponseType).AppendLine(">>();");
            source.AppendLine("        var _result = await _handler.ExecuteAsync(body, cancellationToken).ConfigureAwait(false);");
            source.Append("        if (_result is null) return global::Microsoft.AspNetCore.Http.Results.StatusCode(")
                .Append(endpoint.NullResultStatusCode == 0 ? "204" : endpoint.NullResultStatusCode.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
            EmitResponseETag(source, endpoint, "_result");
            source.Append("        return global::Microsoft.AspNetCore.Http.Results.Json(_result, statusCode: ")
                .Append(endpoint.SuccessStatusCode.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
        }

        source.AppendLine("        }");
        source.AppendLine("        catch (global::System.OperationCanceledException)");
        source.AppendLine("        {");
        source.AppendLine("            throw;");
        source.AppendLine("        }");
        source.AppendLine("        catch (global::System.Exception _exception)");
        source.AppendLine("        {");
        source.AppendLine("            return global::Ark.MediatorFramework.AzureFunctions.ArkAzureFunctionsResults.FromException(_exception);");
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static void EmitResponseETag(StringBuilder source, Endpoint endpoint, string resultName)
    {
        if (endpoint.ResponseETagProperty is null)
            return;

        source.Append("        var _etagResult = global::Ark.MediatorFramework.AzureFunctions.ArkAzureFunctionsResults.ApplyResponseETag(request.HttpContext, ")
            .Append(resultName).Append('.').Append(endpoint.ResponseETagProperty)
            .Append(", ").Append(endpoint.Verb == "GET" ? "true" : "false").AppendLine(");");
        source.AppendLine("        if (_etagResult is not null) return _etagResult;");
    }

    private readonly record struct HostInfo(
        INamedTypeSymbol Marker,
        string Prefix,
        ImmutableArray<INamedTypeSymbol> Included,
        ImmutableArray<INamedTypeSymbol> Excluded,
        bool MarkerIsInSource);

    private readonly record struct EndpointCandidate(INamedTypeSymbol Type, AttributeData? Attribute);

    private static Endpoint? CreateEndpoint(
        INamedTypeSymbol type,
        AttributeData attribute,
        string prefix)
    {
        if (attribute.ConstructorArguments.ElementAtOrDefault(0).Value is not string verb
            || attribute.ConstructorArguments.ElementAtOrDefault(1).Value is not string template
            || string.IsNullOrWhiteSpace(verb)
            || string.IsNullOrWhiteSpace(template))
            return null;

        var versioning = type.GetAttributes()
            .FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == VersioningAttribute);
        var introduced = GetNamedInt(versioning, "Introduced", 1);
        var retired = GetNamedInt(versioning, "Retired", 0);
        var messagePack = GetNamedBool(attribute, "AcceptsMessagePack");
        var successStatusCode = GetNamedInt(attribute, "SuccessStatusCode", 200);
        var nullResultStatusCode = GetNamedInt(attribute, "NullResultStatusCode", 0);
        var kind = HandlerKind.None;
        string? responseType = null;
        INamedTypeSymbol? responseSymbol = null;
        foreach (var iface in type.AllInterfaces)
        {
            var definition = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (definition == SolidRequest)
            {
                kind = HandlerKind.Request;
                responseType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                responseSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
            if (definition == SolidQuery)
            {
                kind = HandlerKind.Query;
                responseType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                responseSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
            if (definition == SolidCommand)
            {
                kind = HandlerKind.Command;
                break;
            }
        }
        if (kind == HandlerKind.None)
            return null;

        // Extract route parameter names from the template
        var routeNames = new HashSet<string>(
            _routeParamRegex.Matches(template!)
                .Cast<Match>()
                .Select(m => m.Groups["param"].Value)
                .Where(n => !string.Equals(n, "version", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        // Extract per-property binding info at generation time (no runtime reflection per request)
        var properties = AllProperties(type)
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic
                && p.SetMethod is { DeclaredAccessibility: Accessibility.Public })
            .Select(p =>
            {
                var routeAttr = p.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == HttpRouteAttribute);
                var bindingName = routeAttr?.ConstructorArguments.FirstOrDefault().Value as string ?? p.Name;
                var isRoute = routeAttr is not null || routeNames.Contains(p.Name);
                var isQuery = p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == HttpQueryAttribute);
                var isServerSet = p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ServerSetAttribute);
                var isETag = p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ETagAttribute);
                var isString = p.Type.SpecialType == SpecialType.System_String;
                return new PropertyInfo(
                    p.Name,
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    isRoute,
                    bindingName,
                    isQuery,
                    isServerSet,
                    isString,
                    isETag);
            })
            .ToImmutableArray();
        var responseETagProperty = responseSymbol is null
            ? null
            : AllProperties(responseSymbol)
                .FirstOrDefault(property => property.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == ETagAttribute))
                ?.Name;

        return new Endpoint(
            type.Name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            verb.ToUpperInvariant(),
            string.Empty,
            string.Empty,
            messagePack,
            type.Locations.FirstOrDefault(),
            prefix,
            template,
            Math.Max(1, introduced),
            retired,
            kind,
            responseType ?? "global::System.Void",
            properties,
            GetNamedBool(attribute, "AllowAnonymous"),
            successStatusCode,
            nullResultStatusCode,
            responseETagProperty);
    }

    private static bool IsSelected(
        INamedTypeSymbol type,
        IAssemblySymbol assembly,
        ImmutableArray<INamedTypeSymbol> included,
        ImmutableArray<INamedTypeSymbol> excluded)
    {
        if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly))
            return false;
        if (excluded.Any(item => SymbolEqualityComparer.Default.Equals(item, type)))
            return false;
        return included.IsDefaultOrEmpty || included.Any(item => SymbolEqualityComparer.Default.Equals(item, type));
    }

    private static ImmutableArray<INamedTypeSymbol> GetTypes(AttributeData attribute, string name)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        if (argument.Kind != TypedConstantKind.Array)
            return ImmutableArray<INamedTypeSymbol>.Empty;
        return argument.Values
            .Where(value => value.Value is INamedTypeSymbol)
            .Select(value => (INamedTypeSymbol)value.Value!)
            .ToImmutableArray();
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol space)
    {
        foreach (var member in space.GetMembers())
        {
            if (member is INamespaceSymbol child)
            {
                foreach (var type in AllTypes(child))
                    yield return type;
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<IPropertySymbol> AllProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
                yield return member;
    }

    private static int GetNamedInt(AttributeData? attribute, string name, int fallback)
    {
        if (attribute is null)
            return fallback;

        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value.Value is int number ? number : fallback;
    }

    private static bool GetNamedBool(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value.Value is true;
    }

    private static string Combine(string prefix, string template)
    {
        return prefix.TrimEnd('/') + "/" + template.TrimStart('/');
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string ExpandRoute(string prefix, string template, int version)
    {
        var versionText = version.ToString(CultureInfo.InvariantCulture);
        var route = template.Contains("{version}", StringComparison.OrdinalIgnoreCase)
            ? template.Replace("{version}", versionText)
            : Combine(prefix.Replace("{version}", versionText), template);
        return route.Trim('/');
    }

    private readonly record struct PropertyInfo(
        string Name,
        string TypeFullName,
        bool IsRoute,
        string BindingName,
        bool IsQuery,
        bool IsServerSet,
        bool IsString,
        bool IsETag);

    private readonly record struct Endpoint(
        string TypeName,
        string FullyQualifiedType,
        string Verb,
        string Route,
        string FunctionName,
        bool MessagePack,
        Location? Location,
        string Prefix,
        string Template,
        int Introduced,
        int Retired,
        HandlerKind Kind,
        string ResponseType,
        ImmutableArray<PropertyInfo> Properties,
        bool AllowAnonymous,
        int SuccessStatusCode,
        int NullResultStatusCode,
        string? ResponseETagProperty);

    private enum HandlerKind
    {
        None,
        Request,
        Query,
        Command
    }
}
