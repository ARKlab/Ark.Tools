// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.ApiSurface;

/// <summary>Generates the deterministic transport API surface consumed by the build target.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ApiSurfaceGenerator : IIncrementalGenerator
{
    private const string Http = "Ark.MediatorFramework.HttpEndpointAttribute";
    private const string Grpc = "Ark.MediatorFramework.GrpcMethodAttribute";
    private const string GrpcService = "Ark.MediatorFramework.GrpcServiceAttribute";
    private const string Rebus = "Ark.MediatorFramework.RebusMessageAttribute";
    private const string ApiGroup = "Ark.MediatorFramework.ApiGroupAttribute";
    private const string ServerSet = "Ark.MediatorFramework.ServerSetAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, static (spc, compilation) =>
        {
            var lines = new List<string>();
            foreach (var type in AllTypes(compilation.Assembly.GlobalNamespace))
                AddType(lines, type);

            var orderedLines = lines.Distinct(StringComparer.Ordinal).OrderBy(line => line, StringComparer.Ordinal).ToArray();
            var text = string.Join("\n", orderedLines) + (orderedLines.Length == 0 ? string.Empty : "\n");
            spc.AddSource("ArkApiSurface.g.cs", "/*\n" + text.Replace("*/", "* /") + "*/\n");
        });
    }

    private static void AddType(List<string> lines, INamedTypeSymbol type)
    {
        var http = Attribute(type, Http);
        var grpc = Attribute(type, Grpc);
        var rebus = Attribute(type, Rebus);
        if (http is null && grpc is null && rebus is null)
            return;

        var request = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var result = ResultType(type);
        var metadata = new List<string>();
        var group = StringArgument(Attribute(type, ApiGroup), 0) ?? "Ark";
        var grpcGroup = StringArgument(Attribute(type, GrpcService), 0);
        if (grpcGroup is not null)
            metadata.Add($"grpc-group={grpcGroup}");
        if (http is not null)
        {
            var introduced = IntArgument(http, "IntroducedIn", 1);
            var retired = IntArgument(http, "RetiredIn", 0);
            metadata.Add($"http={StringArgument(http, 0)} {StringArgument(http, 1)}");
            metadata.Add($"version={introduced}{(retired == 0 ? "+" : $"-{retired - 1}")}");
        }

        if (grpc is not null)
        {
            var introduced = IntArgument(grpc, "IntroducedIn", 1);
            var retired = IntArgument(grpc, "RetiredIn", 0);
            metadata.Add($"grpc={StringArgument(grpc, 0) ?? type.Name}");
            metadata.Add($"grpc-version={introduced}{(retired == 0 ? "+" : $"-{retired - 1}")}");
        }

        lines.Add($"CONTRACT {request} -> {TypeName(result)} [group={group}]"
            + (metadata.Count == 0 ? string.Empty : " [" + string.Join("] [", metadata) + "]"));

        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
            AddContract(lines, request, member, string.Empty, visited);
        if (result is INamedTypeSymbol resultType && resultType.TypeKind == TypeKind.Class
            && !SymbolEqualityComparer.Default.Equals(resultType, type))
        {
            lines.Add($"CONTRACT {resultType.Name}");
            foreach (var member in resultType.GetMembers().OfType<IPropertySymbol>())
                AddContract(lines, resultType.Name, member, string.Empty, visited);
        }

        if (rebus is not null)
            lines.Add($"REBUS {request} -> queue:{StringNamed(rebus, "OwnerQueue") ?? "default"}");
    }

    private static void AddContract(List<string> lines, string owner, IPropertySymbol property, string prefix, HashSet<ITypeSymbol> visited)
    {
        if (property.Name == "EqualityContract")
            return;
        var path = prefix + property.Name;
        var serverSet = Attribute(property, ServerSet) is not null;
        var type = Unwrap(property.Type, out var collection);
        if (type is INamedTypeSymbol named && named.TypeKind == TypeKind.Class && named.SpecialType == SpecialType.None
            && named.ContainingAssembly.Name == property.ContainingAssembly.Name
            && visited.Add(named))
        {
            foreach (var child in named.GetMembers().OfType<IPropertySymbol>())
                AddContract(lines, owner, child, path + (collection ? "[]." : "."), visited);
            visited.Remove(named);
        }
        else
            lines.Add($"CONTRACT {owner}.{path}{(collection ? "[]" : string.Empty)} : {TypeName(property.Type)}"
                + (serverSet ? " server-set=true" : string.Empty)
                + DefaultValue(property));
    }

    private static ITypeSymbol ResultType(INamedTypeSymbol type)
    {
        var iface = type.AllInterfaces.FirstOrDefault(x => x.Name is "IQuery" or "IRequest" or "ICommand");
        return iface?.TypeArguments.FirstOrDefault() ?? type;
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type, out bool collection)
    {
        collection = type is IArrayTypeSymbol;
        if (type is IArrayTypeSymbol array)
            return array.ElementType;
        if (type is INamedTypeSymbol named && named.IsGenericType
            && named.AllInterfaces.Any(x => x.OriginalDefinition.SpecialType == SpecialType.None && x.Name is "IEnumerable" or "IReadOnlyCollection" or "IReadOnlyList" or "List"))
        {
            collection = true;
            return named.TypeArguments[0];
        }
        return type;
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol child)
                foreach (var type in AllTypes(child))
                    yield return type;
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in AllNestedTypes(type))
                    yield return nested;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in AllNestedTypes(nested))
                yield return child;
        }
    }

    private static AttributeData? Attribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == name);

    private static string? StringArgument(AttributeData? attribute, int index) =>
        attribute?.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as string : null;

    private static string? StringNamed(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as string;

    private static bool BoolNamed(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as bool? == true;

    private static int IntArgument(AttributeData attribute, string? name, int fallback)
    {
        if (name is not null)
            return attribute.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as int? ?? fallback;
        return attribute.ConstructorArguments.Length == 0 ? fallback : attribute.ConstructorArguments[0].Value as int? ?? fallback;
    }

    private static string TypeName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Replace(" ", string.Empty);

    private static string DefaultValue(IPropertySymbol property)
    {
        var syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is not Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax declaration
            || declaration.Initializer is null)
            return string.Empty;

        return $" default={declaration.Initializer.Value}";
    }
}
