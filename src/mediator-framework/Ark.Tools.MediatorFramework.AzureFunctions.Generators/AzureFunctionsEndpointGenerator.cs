// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (productionContext, compilation) => Emit(productionContext, compilation));
    }

    private static void Emit(SourceProductionContext context, Compilation compilation)
    {
        var hostAttribute = compilation.GetTypeByMetadataName(HostAttribute);
        var endpointAttribute = compilation.GetTypeByMetadataName(EndpointAttribute);
        if (hostAttribute is null || endpointAttribute is null)
            return;

        var hosts = compilation.Assembly.GetAttributes()
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, hostAttribute))
            .ToArray();
        if (hosts.Length == 0)
            return;

        var endpoints = new List<Endpoint>();
        foreach (var host in hosts)
        {
            if (host.ConstructorArguments.Length < 2
                || host.ConstructorArguments[0].Value is not INamedTypeSymbol marker
                || host.ConstructorArguments[1].Value is not string prefix)
                continue;

            var included = GetTypes(host, "IncludedContracts");
            var excluded = GetTypes(host, "ExcludedContracts");
            foreach (var type in AllTypes(marker.ContainingAssembly.GlobalNamespace))
            {
                var attribute = type.GetAttributes()
                    .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, endpointAttribute));
                if (attribute is null || !IsSelected(type, marker.ContainingAssembly, included, excluded))
                    continue;

                var endpoint = CreateEndpoint(type, attribute, prefix, compilation);
                if (endpoint is not null)
                    endpoints.Add(endpoint.Value);
            }
        }

        var valid = new List<Endpoint>();
        foreach (var endpoint in endpoints.OrderBy(item => item.FunctionName, StringComparer.Ordinal))
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

    private static Endpoint? CreateEndpoint(
        INamedTypeSymbol type,
        AttributeData attribute,
        string prefix,
        Compilation compilation)
    {
        var verb = attribute.ConstructorArguments.ElementAtOrDefault(0).Value as string;
        var template = attribute.ConstructorArguments.ElementAtOrDefault(1).Value as string;
        if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(template))
            return null;

        var versioning = type.GetAttributes()
            .FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == VersioningAttribute);
        var introduced = GetNamedInt(versioning, "Introduced", 1);
        var retired = GetNamedInt(versioning, "Retired", 0);
        var version = Math.Max(1, introduced);
        if (retired > 0 && version >= retired)
            return null;

        var route = template.Contains("{version}", StringComparison.OrdinalIgnoreCase)
            ? template.Replace("{version}", version.ToString(), StringComparison.OrdinalIgnoreCase)
            : Combine(prefix.Replace("{version}", version.ToString(), StringComparison.OrdinalIgnoreCase), template);
        route = route.Trim('/');

        var messagePack = GetNamedBool(attribute, "AcceptsMessagePack");
        var functionName = Sanitize(type.Name + "_v" + version);
        return new Endpoint(
            type.Name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            verb.ToUpperInvariant(),
            route,
            functionName,
            messagePack,
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
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
        var value = attribute?.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value?.Value is int number ? number : fallback;
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

    private readonly record struct Endpoint(
        string TypeName,
        string FullyQualifiedType,
        string Verb,
        string Route,
        string FunctionName,
        bool MessagePack,
        Location? Location);
}
