// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.ApiSurface;

/// <summary>Generates the deterministic transport API surface and emits per-contract diagnostics when the snapshot drifts.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ApiSurfaceGenerator : IIncrementalGenerator
{
    private const string Http = "Ark.MediatorFramework.HttpEndpointAttribute";
    private const string Grpc = "Ark.MediatorFramework.GrpcMethodAttribute";
    private const string GrpcService = "Ark.MediatorFramework.GrpcServiceAttribute";
    private const string Rebus = "Ark.MediatorFramework.RebusMessageAttribute";
    private const string ApiGroup = "Ark.MediatorFramework.ApiGroupAttribute";
    private const string ServerSet = "Ark.MediatorFramework.ServerSetAttribute";
    private const string Versioning = "Ark.MediatorFramework.VersioningAttribute";

    private static readonly DiagnosticDescriptor MissingSnapshot = new(
        "ARKAPI001",
        "API surface snapshot missing",
        "ArkApiSurface.txt is missing. Run 'dotnet build -p:EmitCompilerGeneratedFiles=true' to generate ArkApiSurface.current.txt, copy it to $(MSBuildProjectDirectory)/ArkApiSurface.txt, and commit it.",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ContractChanged = new(
        "ARKAPI002",
        "API surface contract changed",
        "Contract '{0}' has changed since the last accepted snapshot. Run 'dotnet build -p:EmitCompilerGeneratedFiles=true' to inspect ArkApiSurface.current.txt, then update ArkApiSurface.txt to accept this change.",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleSnapshots = new(
        "ARKAPI003",
        "Multiple API surface snapshots",
        "Only one ArkApiSurface.txt baseline is allowed, but {0} were found.",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MalformedSnapshot = new(
        "ARKAPI004",
        "Malformed API surface snapshot",
        "ArkApiSurface.txt contains an invalid snapshot line: '{0}'.",
        "Ark.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var httpTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Http,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var grpcTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Grpc,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var rebusTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                Rebus,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();
        var contractTypes = httpTypes.Combine(grpcTypes).Combine(rebusTypes)
            .Select(static (pair, _) =>
            {
                var ((http, grpc), rebus) = pair;
                return http.AddRange(grpc).AddRange(rebus);
            });
        var surfaceProvider = contractTypes.Select(static (types, cancellationToken) =>
            BuildSurface(types, cancellationToken));

        // Emit the .g.cs snapshot file (unchanged behaviour)
        context.RegisterSourceOutput(surfaceProvider, static (spc, surface) =>
        {
            var (lines, _) = surface;
            var text = string.Join("\n", lines) + (lines.Length == 0 ? string.Empty : "\n");
            spc.AddSource("ArkApiSurface.g.cs", "/*\n" + text.Replace("*/", "* /") + "*/\n");
        });

        // Read baseline from AdditionalFiles
        var baselineProvider = context.AdditionalTextsProvider
            .Where(static f => string.Equals(Path.GetFileName(f.Path), "ArkApiSurface.txt", StringComparison.OrdinalIgnoreCase))
            .Collect();

        // Read opt-out flag from MSBuild global properties
        var enabledProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (opts, _) =>
            {
                opts.GlobalOptions.TryGetValue("build_property.ArkApiSurfaceEnabled", out var v);
                // Only enable when the MSBuild property is explicitly set to "true"
                // (propagated via CompilerVisibleProperty in the buildTransitive .targets).
                // Absent means the project did not opt in or the .targets was not imported.
                return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            });

        // Emit per-contract diagnostics when baseline drifts
        context.RegisterSourceOutput(
            surfaceProvider.Combine(baselineProvider).Combine(enabledProvider),
            static (spc, combined) =>
            {
                var ((surface, baselineFiles), isEnabled) = combined;
                var (currentLines, locations) = surface;
                if (!isEnabled)
                    return;

                if (baselineFiles.Length > 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MultipleSnapshots, Location.None, baselineFiles.Length));
                    return;
                }

                if (baselineFiles.IsEmpty)
                {
                    // Only require a snapshot when there are actual contracts to track
                    if (currentLines.Length > 0)
                        spc.ReportDiagnostic(Diagnostic.Create(MissingSnapshot, Location.None));
                    return;
                }

                var baselineText = baselineFiles[0].GetText(spc.CancellationToken)?.ToString() ?? string.Empty;
                var parsedBaseline = ParseSnapshotLines(baselineText);
                if (!parsedBaseline.IsValid)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        MalformedSnapshot,
                        Location.None,
                        parsedBaseline.InvalidLine));
                    return;
                }

                var baselineSet = parsedBaseline.Lines;
                var currentSet = new HashSet<string>(currentLines, StringComparer.Ordinal);

                var changedOwners = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var line in currentSet.Except(baselineSet, StringComparer.Ordinal)
                    .Concat(baselineSet.Except(currentSet, StringComparer.Ordinal)))
                    changedOwners.Add(ContractOwner(line));

                foreach (var name in changedOwners)
                {
                    var loc = locations.TryGetValue(name, out var l) ? l : Location.None;
                    spc.ReportDiagnostic(Diagnostic.Create(ContractChanged, loc, name));
                }
            });
    }

    // Builds the sorted, deduplicated surface lines and a contract-name → Location index.
    private static (ImmutableArray<string> Lines, ImmutableDictionary<string, Location> Locations) BuildSurface(
        ImmutableArray<INamedTypeSymbol> contractTypes,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var locBuilder = ImmutableDictionary.CreateBuilder<string, Location>(StringComparer.Ordinal);

        foreach (var type in contractTypes
            .GroupBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // ponytail: MinimallyQualifiedFormat == Name for non-generic non-nested types; generic
            // response types are interfaces/collections and never get a CONTRACT header, so mismatch
            // is not reachable in practice. Upgrade path: use FullyQualifiedFormat + strip namespace.
            var key = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (!locBuilder.ContainsKey(key))
                locBuilder[key] = type.Locations.FirstOrDefault() ?? Location.None;
            AddType(lines, type);
        }

        var ordered = lines.Distinct(StringComparer.Ordinal)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToImmutableArray();
        return (ordered, locBuilder.ToImmutable());
    }

    private static SnapshotParseResult ParseSnapshotLines(string text)
    {
        var lines = text.TrimStart('\ufeff')
            .Split('\n')
            .Select(static l => l.TrimEnd('\r'))
            .Where(static l => l.Length > 0 && l != "/*" && l != "*/")
            .ToImmutableArray();
        var invalidLine = lines.FirstOrDefault(static line =>
            !line.StartsWith("CONTRACT ", StringComparison.Ordinal)
            && !line.StartsWith("REBUS ", StringComparison.Ordinal)
            && !line.StartsWith("ENUM ", StringComparison.Ordinal)
            && !line.StartsWith("EVOLVABLE-ENUM ", StringComparison.Ordinal));
        return invalidLine is null
            ? new SnapshotParseResult(new HashSet<string>(lines, StringComparer.Ordinal), true, string.Empty)
            : new SnapshotParseResult(new HashSet<string>(StringComparer.Ordinal), false, invalidLine);
    }

    // Extracts the contract owner name from a snapshot line.
    // "CONTRACT Foo -> ..."   → "Foo"
    // "CONTRACT Foo.Bar : T"  → "Foo"
    // "REBUS Foo -> queue:x"  → "Foo"
    private static string ContractOwner(string line)
    {
        var start = line.IndexOf(' ') + 1;
        if (start <= 0 || start >= line.Length)
            return line;
        var end = line.IndexOfAny(_ownerTerminators, start);
        return end < 0 ? line[start..] : line[start..end];
    }

    private static readonly char[] _ownerTerminators = { ' ', '.', '[' };

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
        var versioning = Attribute(type, Versioning);
        var introduced = IntArgument(versioning, "Introduced", 1);
        var retired = IntArgument(versioning, "Retired", 0);
        var grpcGroup = StringArgument(Attribute(type, GrpcService), 0);
        if (grpcGroup is not null)
            metadata.Add($"grpc-group={grpcGroup}");
        if (http is not null)
        {
            metadata.Add($"http={StringArgument(http, 0)} {StringArgument(http, 1)}");
            metadata.Add($"version={introduced}{(retired == 0 ? "+" : $"-{retired - 1}")}");
        }

        if (grpc is not null)
        {
            metadata.Add($"grpc={StringArgument(grpc, 0) ?? type.Name}");
            metadata.Add($"grpc-version={introduced}{(retired == 0 ? "+" : $"-{retired - 1}")}");
        }

        lines.Add($"CONTRACT {request} -> {TypeName(result)} [group={group}]"
            + (metadata.Count == 0 ? string.Empty : " [" + string.Join("] [", metadata) + "]"));

        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in AllProperties(type))
            AddContract(lines, request, member, string.Empty, visited);
        if (result is INamedTypeSymbol resultType && resultType.TypeKind == TypeKind.Class
            && !SymbolEqualityComparer.Default.Equals(resultType, type))
        {
            lines.Add($"CONTRACT {resultType.Name}");
            foreach (var member in AllProperties(resultType))
                AddContract(lines, resultType.Name, member, string.Empty, visited);
        }
        else
        {
            // The result type is returned directly (not wrapped in a response class), e.g. an
            // enum or an EvolvableEnum<TEnum>: still emit its explicit member entries.
            AddEnumEntries(lines, result);
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
            foreach (var child in AllProperties(named))
                AddContract(lines, owner, child, path + (collection ? "[]." : "."), visited);
            visited.Remove(named);
        }
        else
        {
            lines.Add($"CONTRACT {owner}.{path}{(collection ? "[]" : string.Empty)} : {TypeName(property.Type)}"
                + (serverSet ? " server-set=true" : string.Empty)
                + DefaultValue(property));
            AddEnumEntries(lines, type);
        }
    }

    private static ITypeSymbol ResultType(INamedTypeSymbol type)
    {
        // Look for IQuery<TResult> or IRequest<TResponse> (the 1-arg base interface that carries the
        // result type). When the type uses the self-generic 2-arg variant such as IQuery<TSelf, TResult>,
        // Roslyn's AllInterfaces also surfaces the 1-arg base, so filtering to TypeArguments.Length == 1
        // correctly resolves the result type for both the legacy and self-generic patterns.
        var resultIface = type.AllInterfaces.FirstOrDefault(
            x => (x.Name is "IQuery" or "IRequest") && x.TypeArguments.Length == 1);
        if (resultIface is not null)
            return resultIface.TypeArguments[0];

        // ICommand / ICommand<TSelf> have no result type; the contract type itself is its own identity.
        return type;
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type, out bool collection)
    {
        collection = type is IArrayTypeSymbol;
        if (type is IArrayTypeSymbol array)
            return array.ElementType;
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1
            && named.AllInterfaces.Any(x => x.OriginalDefinition.SpecialType == SpecialType.None && x.Name is "IEnumerable" or "IReadOnlyCollection" or "IReadOnlyList" or "List"))
        {
            collection = true;
            return named.TypeArguments[0];
        }
        return type;
    }

    // Emits explicit member/value entries for enum types reached from contract members, either
    // used directly ("strict enum") or wrapped in Ark.Tools.Core.EvolvableEnum<TEnum> ("evolvable
    // enum"), so that adding/removing/renumbering members is caught as an API surface drift.
    private static void AddEnumEntries(List<string> lines, ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return;

        if (named.TypeKind == TypeKind.Enum)
        {
            AddEnumMembers(lines, "ENUM", named);
        }
        else if (TryUnwrapEvolvableEnum(named, out var enumType))
        {
            AddEnumMembers(lines, "EVOLVABLE-ENUM", enumType);
        }
    }

    // Detects Ark.Tools.Core.EvolvableEnum<TEnum> by name/arity/namespace (no compile-time
    // reference to Ark.Tools.Core is required, matching the existing attribute-name-matching
    // convention used throughout this generator).
    private static bool TryUnwrapEvolvableEnum(INamedTypeSymbol named, out INamedTypeSymbol enumType)
    {
        if (named.IsGenericType && named.Arity is 1 or 2
            && named.OriginalDefinition.Name == "EvolvableEnum"
            && named.ContainingNamespace?.ToDisplayString() == "Ark.Tools.Core"
            && named.TypeArguments[0] is INamedTypeSymbol argument && argument.TypeKind == TypeKind.Enum)
        {
            enumType = argument;
            return true;
        }

        enumType = null!;
        return false;
    }

    private static void AddEnumMembers(List<string> lines, string kind, INamedTypeSymbol enumType)
    {
        var name = TypeName(enumType);
        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasConstantValue))
            lines.Add($"{kind} {name}.{field.Name}={Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture)}");
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

    private static IEnumerable<IPropertySymbol> AllProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                yield return property;
    }

    private static AttributeData? Attribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == name);

    private static string? StringArgument(AttributeData? attribute, int index) =>
        attribute?.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as string : null;

    private static string? StringNamed(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as string;

    private static bool BoolNamed(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(x => x.Key == name).Value.Value as bool? == true;

    private static int IntArgument(AttributeData? attribute, string? name, int fallback)
    {
        if (attribute is null)
            return fallback;
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

        var value = declaration.Initializer.Value.NormalizeWhitespace().ToFullString()
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("*/", "* /");
        return $" default={value}";
    }

    private readonly record struct SnapshotParseResult(
        HashSet<string> Lines,
        bool IsValid,
        string InvalidLine);
}
