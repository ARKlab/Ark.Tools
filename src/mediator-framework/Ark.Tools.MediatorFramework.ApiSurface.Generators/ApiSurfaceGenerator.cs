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
    private const string ProtoMember = "ProtoBuf.ProtoMemberAttribute";

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
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
            AddContract(lines, request, member, string.Empty, visited);
        if (result is INamedTypeSymbol resultType && resultType.TypeKind == TypeKind.Class)
            foreach (var member in resultType.GetMembers().OfType<IPropertySymbol>())
                AddContract(lines, resultType.Name, member, string.Empty, visited);

        if (http is not null)
        {
            var verb = StringArgument(http, 0) ?? string.Empty;
            var template = StringArgument(http, 1) ?? string.Empty;
            var introduced = IntArgument(http, "IntroducedIn", 1);
            var retired = IntArgument(http, "RetiredIn", 0);
            var lastVersion = retired == 0 ? introduced : retired - 1;
            for (var version = introduced; version <= lastVersion; version++)
            {
                var route = template.Replace("{version}", version.ToString());
                var group = StringArgument(Attribute(type, ApiGroup), 0) ?? "Ark";
                var policy = StringNamed(http, "Policy") ?? (BoolNamed(http, "AllowAnonymous") ? "Anonymous" : "RequireAuthenticatedUser");
                lines.Add($"HTTP {verb} {route} -> {request} : {TypeName(result)} [policy={policy}] [op={type.Name}_{version}] [tag={group}]");
                foreach (var parameter in type.GetMembers().OfType<IPropertySymbol>())
                    if (route.Contains("{" + parameter.Name + "}", StringComparison.Ordinal) || Attribute(parameter, "Ark.MediatorFramework.BindFromQueryAttribute") is not null)
                        lines.Add($"HTTP-PARAM {request}.{parameter.Name} : {TypeName(parameter.Type)} {(route.Contains("{" + parameter.Name + "}", StringComparison.Ordinal) ? "route" : "query")} required");
            }
        }

        if (grpc is not null)
        {
            var name = StringArgument(grpc, 0) ?? type.Name;
            var service = StringArgument(Attribute(type, GrpcService), 0) ?? StringArgument(Attribute(type, ApiGroup), 0) ?? "Ark";
            var introduced = IntArgument(grpc, "IntroducedIn", 1);
            var retired = IntArgument(grpc, "RetiredIn", 0);
            var lastVersion = retired == 0 ? introduced : retired - 1;
            var grpcVisited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            for (var version = introduced; version <= lastVersion; version++)
            {
                lines.Add($"GRPC {service}.V{version}/{name} ({request}) returns ({TypeName(result)}) unary");
                AddProtoFields(lines, request, type, grpcVisited);
            }
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
            lines.Add($"CONTRACT {owner}.{path}{(collection ? "[]" : string.Empty)} : {TypeName(property.Type)} server-set={serverSet.ToString().ToLowerInvariant()}");
    }

    private static void AddProtoFields(List<string> lines, string owner, INamedTypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type))
            return;
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            var proto = Attribute(property, ProtoMember);
            if (proto is null)
                continue;
            var number = IntArgument(proto, null, 0);
            var nested = Unwrap(property.Type, out var collection);
            if (nested is INamedTypeSymbol named && named.TypeKind == TypeKind.Class && named.SpecialType == SpecialType.None)
                AddProtoFields(lines, owner + "." + property.Name + (collection ? "[]" : string.Empty), named, visited);
            else
                lines.Add($"GRPC-FIELD {owner}.{property.Name}{(collection ? "[]" : string.Empty)} = {number} : {TypeName(property.Type)}");
        }
        visited.Remove(type);
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
}
