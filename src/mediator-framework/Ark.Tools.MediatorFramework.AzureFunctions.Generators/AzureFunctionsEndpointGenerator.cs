// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

using Ark.Tools.MediatorFramework.Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Generators;

/// <summary>Generates isolated-worker HTTP triggers for selected mediator contracts.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class AzureFunctionsEndpointGenerator : IIncrementalGenerator
{
    private const string _hostAttribute = "Ark.Tools.MediatorFramework.HttpHostAttribute";
    private const string _hostAttributeStage = "AzureFunctionsHostAttributes";
    private const string _hostSpecStage = "AzureFunctionsHostSpecs";
    private const string _endpointAttributeStage = "AzureFunctionsEndpointAttributes";
    private const string _endpointSpecStage = "AzureFunctionsEndpointSpecs";
    private const string _specStage = "AzureFunctionsSpecs";
    private const string _outputStage = "AzureFunctionsOutput";

    private static readonly DiagnosticDescriptor _messagePackNotSupported = new(
        "ARKMF030",
        "MessagePack is not supported by Azure Functions",
        "HTTP endpoint '{0}' enables MessagePack and cannot be selected by an Azure Functions host",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF030.md");

    private static readonly DiagnosticDescriptor _duplicateRoute = new(
        "ARKMF031",
        "Duplicate Azure Functions route",
        "HTTP endpoints '{0}' and '{1}' resolve to the same Azure Functions route '{2}'",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF031.md");

    private static readonly DiagnosticDescriptor _duplicateFunction = new(
        "ARKMF032",
        "Duplicate Azure Functions name",
        "HTTP endpoints '{0}' and '{1}' resolve to the same Azure Functions name '{2}'",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF032.md");

    private static readonly DiagnosticDescriptor _invalidHostPrefix = new(
        "ARKMF047",
        "Invalid Azure Functions host version prefix",
        "HTTP host version prefix '{0}' must contain the '{{version}}' token",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF047.md");

    private static readonly DiagnosticDescriptor _conflictingHostPrefixes = new(
        "ARKMF048",
        "Conflicting Azure Functions host version prefixes",
        "HTTP host markers for contract assembly '{0}' declare conflicting version prefixes '{1}' and '{2}'",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF048.md");

    private static readonly DiagnosticDescriptor _invalidHostSelection = new(
        "ARKMF049",
        "Invalid Azure Functions host contract selection",
        "Type '{0}' in the host {1} list is not an [HttpEndpoint] contract declared by assembly '{2}'",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF049.md");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hosts = context.SyntaxProvider.ForAttributeWithMetadataName(
                _hostAttribute,
                static (_, _) => true,
                static (attributeContext, cancellationToken) => AzureFunctionsEndpointParser._readHosts(attributeContext, cancellationToken))
            .WithTrackingName(_hostAttributeStage)
            .SelectMany(static (extracted, _) => extracted)
            .WithTrackingName(_hostSpecStage)
            .Collect();
        var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                AzureFunctionsEndpointParser._endpointAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => attributeContext.TargetSymbol is INamedTypeSymbol type
                    ? AzureFunctionsEndpointParser._readEndpoint(type, attributeContext.Attributes[0])
                    : null)
            .WithTrackingName(_endpointAttributeStage)
            .Where(static endpoint => endpoint is not null)
            .Select(static (endpoint, _) => endpoint!.Value)
            .WithTrackingName(_endpointSpecStage)
            .Collect();
        var specs = hosts.Combine(sourceEndpoints)
            .Select(static (pair, _) => new AzureFunctionsAggregateSpec(
                pair.Left
                    .OrderBy(static host => host.MarkerFullyQualifiedType, StringComparer.Ordinal)
                    .ThenBy(static host => host.Prefix, StringComparer.Ordinal)
                    .ToImmutableArray(),
                pair.Right
                    .OrderBy(static endpoint => endpoint.FullyQualifiedType, StringComparer.Ordinal)
                    .ToImmutableArray()))
            .WithTrackingName(_specStage);
        var output = specs
            .Select(static (spec, _) => spec)
            .WithTrackingName(_outputStage);

        context.RegisterSourceOutput(
            output,
            static (productionContext, spec) =>
                _emit(productionContext, spec.Hosts.Values, spec.Endpoints.Values));
    }

    private static void _emit(
        SourceProductionContext context,
        ImmutableArray<HostSpec> hosts,
        ImmutableArray<EndpointSpec> sourceEndpoints)
    {
        if (hosts.IsDefaultOrEmpty)
            return;

        var endpoints = new List<EndpointSpec>();
        var prefixByAssembly = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var host in hosts
            .OrderBy(static item => item.MarkerFullyQualifiedType, StringComparer.Ordinal)
            .ThenBy(static item => item.Prefix, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var hostLocation = LocationSpec._toLocation(host.Location);
            if (host.Prefix.IndexOf("{version}", StringComparison.Ordinal) < 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(_invalidHostPrefix, hostLocation, host.Prefix));
                continue;
            }

            if (prefixByAssembly.TryGetValue(host.MarkerAssemblyName, out var existingPrefix))
            {
                if (!string.Equals(existingPrefix, host.Prefix, StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        _conflictingHostPrefixes, hostLocation, host.MarkerAssemblyName, existingPrefix, host.Prefix));
                    continue;
                }
            }
            else
            {
                prefixByAssembly.Add(host.MarkerAssemblyName, host.Prefix);
            }

            foreach (var selection in host.InvalidSelections)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _invalidHostSelection, hostLocation, selection.TypeName, selection.ListName, host.MarkerAssemblyName));
            }

            var candidates = host.MarkerIsInSource
                ? sourceEndpoints.AsEnumerable()
                : host.MetadataEndpoints;
            foreach (var candidate in candidates)
            {
                if (!_isSelected(candidate, host))
                    continue;

                endpoints.Add(candidate with { Prefix = host.Prefix });
            }
        }

        var maxVersion = endpoints.Count == 0
            ? 1
            : endpoints.Max(static item => item.Retired > 0 ? item.Retired - 1 : item.Introduced);
        var expanded = endpoints.SelectMany(endpoint =>
            Enumerable.Range(endpoint.Introduced, Math.Max(1, maxVersion - endpoint.Introduced + 1))
                .Where(version => endpoint.Retired == 0 || version < endpoint.Retired)
                .Select(version => endpoint with
                {
                    Route = _expandRoute(endpoint.Prefix, endpoint.Template, version),
                    FunctionName = _sanitize(endpoint.TypeName + "_v" + version.ToString(CultureInfo.InvariantCulture)),
                }));

        var valid = new List<EndpointSpec>();
        var routeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var functionOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var endpoint in expanded
            .OrderBy(static item => item.FunctionName, StringComparer.Ordinal)
            .ThenBy(static item => item.FullyQualifiedType, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var endpointLocation = LocationSpec._toLocation(endpoint.Location);
            if (endpoint.MessagePack)
            {
                context.ReportDiagnostic(Diagnostic.Create(_messagePackNotSupported, endpointLocation, endpoint.TypeName));
                continue;
            }

            var routeKey = endpoint.Verb + "\u0000" + endpoint.Route;
            if (routeOwners.TryGetValue(routeKey, out var routeOwner))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _duplicateRoute, endpointLocation, routeOwner, endpoint.TypeName, endpoint.Route));
                continue;
            }

            if (functionOwners.TryGetValue(endpoint.FunctionName, out var functionOwner))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _duplicateFunction, endpointLocation, functionOwner, endpoint.TypeName, endpoint.FunctionName));
                continue;
            }

            routeOwners.Add(routeKey, endpoint.TypeName);
            functionOwners.Add(endpoint.FunctionName, endpoint.TypeName);
            valid.Add(endpoint);
        }

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("namespace Ark.Tools.MediatorFramework.AzureFunctions.Generated;");
        source.AppendLine();
        source.AppendLine("public static class ArkGeneratedFunctions");
        source.AppendLine("{");
        foreach (var endpoint in valid)
        {
            _emitFunction(source, endpoint);
        }
        _emitHealthCheckFunction(source);
        source.AppendLine("}");
        context.AddSource("ArkGeneratedFunctions.g.cs", source._toGeneratedSource());
    }

    private static bool _isSelected(in EndpointSpec endpoint, in HostSpec host)
    {
        if (!string.Equals(endpoint.AssemblyName, host.MarkerAssemblyName, StringComparison.Ordinal))
            return false;
        if (host.Excluded.Values.Contains(endpoint.FullyQualifiedType, StringComparer.Ordinal))
            return false;
        return host.Included.IsEmpty || host.Included.Values.Contains(endpoint.FullyQualifiedType, StringComparer.Ordinal);
    }

    private static void _emitFunction(StringBuilder source, EndpointSpec endpoint)
    {
        var hasBody = endpoint.Verb is "POST" or "PUT" or "PATCH";
        var routeProperties = endpoint.Properties.Where(static p => p.IsRoute && !p.IsServerSet).ToArray();
        var queryProperties = endpoint.Properties.Where(static p => p.IsQuery && !p.IsServerSet).ToArray();
        var serverSetProperties = endpoint.Properties.Where(static p => p.IsServerSet).ToArray();
        var attachment = endpoint.Properties.FirstOrDefault(static p => p.IsAttachment || p.IsAttachmentCollection);
        var hasAttachment = attachment.IsAttachment || attachment.IsAttachmentCollection;

        source.Append("    [global::Microsoft.Azure.Functions.Worker.Function(\"")
            .Append(endpoint.FunctionName).AppendLine("\")]");
        source.Append("    public static async global::System.Threading.Tasks.Task<global::Microsoft.AspNetCore.Http.IResult> ")
            .Append(endpoint.FunctionName).AppendLine("(");
        source.Append("        [global::Microsoft.Azure.Functions.Worker.HttpTrigger(")
            .Append("global::Microsoft.Azure.Functions.Worker.AuthorizationLevel.Anonymous, \"")
            .Append(endpoint.Verb.ToLowerInvariant()).Append("\", Route = \"")
            .Append(_escape(endpoint.Route)).AppendLine("\")]");
        source.AppendLine("        global::Microsoft.AspNetCore.Http.HttpRequest request,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
        source.AppendLine("    {");
        source.Append("        var _authentication = await global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsInvocation.AuthenticateAsync(request.HttpContext, ")
            .Append(endpoint.AllowAnonymous ? "true" : "false")
            .AppendLine(").ConfigureAwait(false);");
        source.AppendLine("        if (_authentication is not null)");
        source.AppendLine("            return _authentication;");

        if (endpoint.MaxRequestBodySizeBytes > 0)
        {
            source.Append("        var _sizeLimit = global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsHttp.EnforceMaxRequestBodySize(request, ")
                .Append(endpoint.MaxRequestBodySizeBytes.ToString(CultureInfo.InvariantCulture))
                .AppendLine("L);");
            source.AppendLine("        if (_sizeLimit is not null)");
            source.AppendLine("            return _sizeLimit;");
        }

        // Body or default-instance binding
        if (hasAttachment)
        {
            source.AppendLine("        global::System.Collections.Generic.IReadOnlyList<global::Ark.Tools.MediatorFramework.IArkAttachment> _attachments;");
            source.AppendLine("        try");
            source.AppendLine("        {");
            source.Append("            _attachments = await global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsHttp.ReadAttachmentsAsync(request, ")
                .Append(endpoint.MaxFileCount.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(endpoint.AllowedContentTypes.IsEmpty
                    ? "global::System.Array.Empty<string>()"
                    : "new string[] { " + string.Join(", ", endpoint.AllowedContentTypes.Select(_literal)) + " }")
                .AppendLine(", cancellationToken).ConfigureAwait(false);");
            source.AppendLine("        }");
            source.AppendLine("        catch (global::System.NotSupportedException)");
            source.AppendLine("        {");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.StatusCode(415);");
            source.AppendLine("        }");
            source.AppendLine("        catch (global::System.IO.InvalidDataException _formException)");
            source.AppendLine("        {");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_MULTIPART\", detail: _formException.Message);");
            source.AppendLine("        }");
            source.Append("        if (_attachments.Count ").Append(attachment.IsAttachmentCollection ? " == 0" : " != 1").AppendLine(")");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_FILE_COUNT\", detail: \"The uploaded file count is invalid.\");");
        }

        if (hasBody && !hasAttachment && endpoint.BodyProperty is null)
        {
            source.Append("        ").Append(endpoint.FullyQualifiedType).AppendLine("? _bodyNullable;");
            source.AppendLine("        try");
            source.AppendLine("        {");
            source.Append("            _bodyNullable = await global::Microsoft.AspNetCore.Http.HttpRequestJsonExtensions.ReadFromJsonAsync<").Append(endpoint.FullyQualifiedType).AppendLine(">(request, cancellationToken).ConfigureAwait(false);");
            source.AppendLine("        }");
            source.AppendLine("        catch (global::System.Text.Json.JsonException ex)");
            source.AppendLine("        {");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: ex.Message);");
            source.AppendLine("        }");
            source.AppendLine("        if (_bodyNullable is null)");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: \"Request body is missing or could not be deserialized.\");");
            source.AppendLine("        var body = _bodyNullable;");
        }
        else if (hasBody && !hasAttachment)
        {
            source.Append("        ").Append(endpoint.BodyType).AppendLine("? _bodyNullable;");
            source.AppendLine("        try");
            source.AppendLine("        {");
            source.Append("            _bodyNullable = await global::Microsoft.AspNetCore.Http.HttpRequestJsonExtensions.ReadFromJsonAsync<").Append(endpoint.BodyType).AppendLine(">(request, cancellationToken).ConfigureAwait(false);");
            source.AppendLine("        }");
            source.AppendLine("        catch (global::System.Text.Json.JsonException ex)");
            source.AppendLine("        {");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: ex.Message);");
            source.AppendLine("        }");
            source.AppendLine("        if (_bodyNullable is null)");
            source.AppendLine("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: \"Request body is missing or could not be deserialized.\");");
            source.Append("        var body = ").Append(_constructEnvelope(endpoint, endpoint.BodyProperty!, "_bodyNullable")).AppendLine(";");
        }
        else if (!hasAttachment)
        {
            source.Append("        var body = ").Append(_constructEnvelope(endpoint, null, null)).AppendLine(";");
        }
        else
        {
            source.Append("        var body = ").Append(_constructEnvelope(
                endpoint,
                attachment.Name,
                attachment.IsAttachmentCollection ? "_attachments" : "_attachments[0]")).AppendLine(";");
        }

        // Route value binding (per-property, no runtime reflection)
        foreach (var prop in routeProperties)
        {
            if (prop.IsString)
            {
                _emitPropertyAssignment(source, endpoint, "        ", prop.Name,
                    "request.RouteValues[" + _literal(prop.BindingName) + "]?.ToString()!");
            }
            else
            {
                var varName = "_route_" + prop.Name;
                source.Append("        if (!global::Ark.Tools.Core.ArkTypeConverter.TryConvertSafe<").Append(prop.TypeFullName).Append(">(request.RouteValues[").Append(_literal(prop.BindingName)).Append("]?.ToString(), out var ").Append(varName).AppendLine("))");
                source.Append("            return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"BINDING_FAILURE\", detail: \"Route value '").Append(prop.BindingName).Append("' could not be bound to type '").Append(prop.TypeFullName).AppendLine("'.\");");
                _emitPropertyAssignment(source, endpoint, "        ", prop.Name, varName);
            }
        }

        // Query string binding (per-property, no runtime reflection)
        foreach (var prop in queryProperties)
        {
            source.Append("        if (request.Query.TryGetValue(").Append(_literal(prop.Name)).Append(", out var _qs_").Append(prop.Name).AppendLine("))");
            source.AppendLine("        {");
            if (prop.IsString)
            {
                _emitPropertyAssignment(source, endpoint, "            ", prop.Name,
                    "((string?)_qs_" + prop.Name + ")!");
            }
            else
            {
                var varName = "_query_" + prop.Name;
                source.Append("            if (!global::Ark.Tools.Core.ArkTypeConverter.TryConvertSafe<").Append(prop.TypeFullName).Append(">(_qs_").Append(prop.Name).Append(", out var ").Append(varName).AppendLine("))");
                source.Append("                return global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"BINDING_FAILURE\", detail: \"Query value '").Append(prop.Name).Append("' could not be bound to type '").Append(prop.TypeFullName).AppendLine("'.\");");
                _emitPropertyAssignment(source, endpoint, "            ", prop.Name, varName);
            }
            source.AppendLine("        }");
        }

        // Server-set property reset (per-property, no runtime reflection)
        foreach (var prop in serverSetProperties)
        {
            _emitPropertyAssignment(source, endpoint, "        ", prop.Name, "default!");
        }
        var _etagProperties = endpoint.Properties.Where(static p => p.IsETag).ToArray();
        if (_etagProperties.Length > 0)
        {
            source.AppendLine("        var _etag = global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsResults.ReadPrecondition(request.HttpContext);");
            foreach (var prop in _etagProperties)
            {
                source.AppendLine("        if (_etag is not null)");
                _emitPropertyAssignment(source, endpoint, "            ", prop.Name, "_etag");
            }
        }

        // Dispatch via the ASP.NET Core service provider.
        source.AppendLine("        var _services = request.HttpContext.RequestServices;");
        source.AppendLine("        try");
        source.AppendLine("        {");

        if (endpoint.Kind == HandlerKind.Command)
        {
            source.AppendLine("        var _processor = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.ICommandProcessor>(_services);");
            source.Append("        await _processor.ExecuteAsync<").Append(endpoint.FullyQualifiedType).AppendLine(">(body, cancellationToken).ConfigureAwait(false);");
            source.Append("        return global::Microsoft.AspNetCore.Http.Results.StatusCode(")
                .Append(endpoint.SuccessStatusCode == 200 ? "204" : endpoint.SuccessStatusCode.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
        }
        else if (endpoint.Kind == HandlerKind.Query)
        {
            source.AppendLine("        var _processor = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.IQueryProcessor>(_services);");
            source.Append("        var _result = await _processor.ExecuteAsync<").Append(endpoint.FullyQualifiedType).Append(", ").Append(endpoint.ResponseType).AppendLine(">(body, cancellationToken).ConfigureAwait(false);");
            source.Append("        if (_result is null) return global::Microsoft.AspNetCore.Http.Results.StatusCode(")
                .Append(endpoint.NullResultStatusCode == 0 ? "404" : endpoint.NullResultStatusCode.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
            _emitResponseETag(source, endpoint, "_result");
            _emitResponse(source, endpoint);
        }
        else
        {
            source.AppendLine("        var _processor = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.IRequestProcessor>(_services);");
            source.Append("        var _result = await _processor.ExecuteAsync<").Append(endpoint.FullyQualifiedType).Append(", ").Append(endpoint.ResponseType).AppendLine(">(body, cancellationToken).ConfigureAwait(false);");
            source.Append("        if (_result is null) return global::Microsoft.AspNetCore.Http.Results.StatusCode(")
                .Append(endpoint.NullResultStatusCode == 0 ? "204" : endpoint.NullResultStatusCode.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
            _emitResponseETag(source, endpoint, "_result");
            _emitResponse(source, endpoint);
        }

        source.AppendLine("        }");
        source.AppendLine("        catch (global::System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)");
        source.AppendLine("        {");
        source.AppendLine("            throw;");
        source.AppendLine("        }");
        source.AppendLine("        catch (global::System.Exception _exception)");
        source.AppendLine("        {");
        source.AppendLine("            return global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsResults.FromException(_exception);");
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static void _emitPropertyAssignment(StringBuilder source, EndpointSpec endpoint, string indent, string propertyName, string value)
    {
        if (endpoint.IsRecord)
            source.Append(indent).Append("body = body with { ").Append(propertyName).Append(" = ").Append(value).AppendLine(" };");
        else
            source.Append(indent).Append("body.").Append(propertyName).Append(" = ").Append(value).AppendLine(";");
    }

    private static string _constructEnvelope(EndpointSpec endpoint, string? assignedProperty, string? assignedValue)
    {
        if (endpoint.ConstructorParameters.IsEmpty)
        {
            return assignedProperty is null
                ? "new " + endpoint.FullyQualifiedType + "()"
                : "new " + endpoint.FullyQualifiedType + " { " + assignedProperty + " = " + assignedValue + " }";
        }

        return "new " + endpoint.FullyQualifiedType + "("
            + string.Join(", ", endpoint.ConstructorParameters.Select(parameter =>
                string.Equals(parameter, assignedProperty, StringComparison.OrdinalIgnoreCase)
                    ? assignedValue
                    : "default!"))
            + ")";
    }

    private static void _emitHealthCheckFunction(StringBuilder source)
    {
        source.AppendLine("    [global::Microsoft.Azure.Functions.Worker.Function(\"ArkHealthCheck\")]");
        source.AppendLine("    public static async global::System.Threading.Tasks.Task<global::Microsoft.AspNetCore.Http.IResult> ArkHealthCheck(");
        source.AppendLine("        [global::Microsoft.Azure.Functions.Worker.HttpTrigger(global::Microsoft.Azure.Functions.Worker.AuthorizationLevel.Anonymous, \"get\", Route = \"healthCheck\")] global::Microsoft.AspNetCore.Http.HttpRequest request,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
        source.AppendLine("    {");
        source.AppendLine("        var healthChecks = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>(request.HttpContext.RequestServices);");
        source.AppendLine("        return await global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsHttp.CheckHealthAsync(healthChecks, cancellationToken).ConfigureAwait(false);");
        source.AppendLine("    }");
    }

    private static void _emitResponse(StringBuilder source, EndpointSpec endpoint)
    {
        if (endpoint.ResponseType == "global::Ark.Tools.MediatorFramework.IArkAttachment")
        {
            source.AppendLine("        await global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsHttp.WriteAttachmentAsync(request.HttpContext.Response, _result, cancellationToken).ConfigureAwait(false);");
            source.AppendLine("        return global::Microsoft.AspNetCore.Http.Results.Empty;");
        }
        else if (endpoint.IsStreaming)
        {
            source.AppendLine("        await global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsHttp.WriteJsonStreamAsync(request.HttpContext.Response, _result, cancellationToken).ConfigureAwait(false);");
            source.AppendLine("        return global::Microsoft.AspNetCore.Http.Results.Empty;");
        }
        else
        {
            source.Append("        return global::Microsoft.AspNetCore.Http.Results.Json(_result, statusCode: ")
                .Append(endpoint.SuccessStatusCode.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
        }
    }

    private static void _emitResponseETag(StringBuilder source, EndpointSpec endpoint, string resultName)
    {
        if (endpoint.ResponseETagProperty is null)
            return;

        source.Append("        var _etagResult = global::Ark.Tools.MediatorFramework.AzureFunctions.ArkAzureFunctionsResults.ApplyResponseETag(request.HttpContext, ")
            .Append(resultName).Append('.').Append(endpoint.ResponseETagProperty)
            .Append(", ").Append(endpoint.Verb == "GET" ? "true" : "false").AppendLine(");");
        source.AppendLine("        if (_etagResult is not null) return _etagResult;");
    }

    private static string _combine(string prefix, string template)
    {
        return prefix.TrimEnd('/') + "/" + template.TrimStart('/');
    }

    private static string _sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        return builder.ToString();
    }

    private static string _escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string _literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string _expandRoute(string prefix, string template, int version)
    {
        var versionText = version.ToString(CultureInfo.InvariantCulture);
        var route = template.Contains("{version}", StringComparison.OrdinalIgnoreCase)
            ? template.Replace("{version}", versionText)
            : _combine(prefix.Replace("{version}", versionText), template);
        return route.Trim('/');
    }

    private sealed record AzureFunctionsAggregateSpec(
        EquatableArray<HostSpec> Hosts,
        EquatableArray<EndpointSpec> Endpoints);
}
