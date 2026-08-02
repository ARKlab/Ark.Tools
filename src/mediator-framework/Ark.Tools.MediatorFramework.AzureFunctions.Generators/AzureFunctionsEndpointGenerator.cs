// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.AzureFunctions.Generators;

/// <summary>Generates isolated-worker HTTP triggers for selected mediator contracts.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class AzureFunctionsEndpointGenerator : IIncrementalGenerator
{
    private const string HostAttribute = "Ark.MediatorFramework.HttpHostAttribute";
    private const string EndpointAttribute = "Ark.MediatorFramework.HttpEndpointAttribute";
    private const string VersioningAttribute = "Ark.MediatorFramework.VersioningAttribute";

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
            source.Append("        return await global::Ark.MediatorFramework.AzureFunctions.ArkAzureFunctionsInvocation.InvokeAsync<")
                .Append(endpoint.FullyQualifiedType).AppendLine(">(request, cancellationToken).ConfigureAwait(false);");
            source.AppendLine("    }");
        }
        source.AppendLine("}");
        context.AddSource("ArkGeneratedFunctions.g.cs", source.ToString());
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
            retired);
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

    private static string ExpandRoute(string prefix, string template, int version)
    {
        var versionText = version.ToString(CultureInfo.InvariantCulture);
        var route = template.Contains("{version}", StringComparison.OrdinalIgnoreCase)
            ? template.Replace("{version}", versionText)
            : Combine(prefix.Replace("{version}", versionText), template);
        return route.Trim('/');
    }

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
        int Retired);
}
