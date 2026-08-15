// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Solid.Analyzers;

/// <summary>
/// Reports queries, requests and commands that implement the legacy single-generic interfaces
/// instead of the self-referencing generic variants that enable reflection-free dispatch.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelfGenericInterfaceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id reported by this analyzer.</summary>
    public const string DiagnosticId = "ARKSOLID001";

    internal static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Use the self-referencing generic interface for reflection-free dispatch",
        "Type '{0}' should implement '{1}' to enable reflection-free processor dispatch",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Implementing the self-referencing generic interface (e.g. IQuery<TSelf, TResult>) allows the processor to resolve both the concrete type and the result type at compile time, avoiding reflection and runtime caches.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var compilation = startContext.Compilation;
            var query1 = compilation.GetTypeByMetadataName("Ark.Tools.Solid.IQuery`1");
            var query2 = compilation.GetTypeByMetadataName("Ark.Tools.Solid.IQuery`2");
            var request1 = compilation.GetTypeByMetadataName("Ark.Tools.Solid.IRequest`1");
            var request2 = compilation.GetTypeByMetadataName("Ark.Tools.Solid.IRequest`2");
            var command0 = compilation.GetTypeByMetadataName("Ark.Tools.Solid.ICommand");
            var command1 = compilation.GetTypeByMetadataName("Ark.Tools.Solid.ICommand`1");

            if (query2 is null && request2 is null && command1 is null)
                return;

            startContext.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    return;
                if (type.IsAbstract)
                    return;

                _checkGeneric(symbolContext, type, query1, query2, "IQuery");
                _checkGeneric(symbolContext, type, request1, request2, "IRequest");
                _checkCommand(symbolContext, type, command0, command1);
            }, SymbolKind.NamedType);
        });
    }

    private static void _checkGeneric(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol? legacyDefinition,
        INamedTypeSymbol? selfDefinition,
        string interfaceName)
    {
        if (legacyDefinition is null || selfDefinition is null)
            return;

        var legacy = type.AllInterfaces.FirstOrDefault(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, legacyDefinition));
        if (legacy is null)
            return;

        if (legacy.TypeArguments.Length != 1
            || legacy.TypeArguments[0].Kind == SymbolKind.ErrorType)
            return;

        if (_implementsSelf(type, selfDefinition))
            return;

        var resultType = legacy.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var selfType = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        _report(context, type, $"{interfaceName}<{selfType}, {resultType}>");
    }

    private static void _checkCommand(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol? commandDefinition,
        INamedTypeSymbol? selfDefinition)
    {
        if (commandDefinition is null || selfDefinition is null)
            return;

        if (!type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, commandDefinition)))
            return;

        if (_implementsSelf(type, selfDefinition))
            return;

        _report(context, type, $"ICommand<{type.Name}>");
    }

    private static bool _implementsSelf(INamedTypeSymbol type, INamedTypeSymbol selfDefinition)
    {
        return type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, selfDefinition)
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type));
    }

    private static void _report(SymbolAnalysisContext context, INamedTypeSymbol type, string suggested)
    {
        var location = type.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(_rule, location, type.Name, suggested));
    }
}
