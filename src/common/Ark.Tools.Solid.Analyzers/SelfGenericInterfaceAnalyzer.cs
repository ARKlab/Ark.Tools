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
        "Type '{0}' must implement '{1}' to enable reflection-free processor dispatch",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Implementing the self-referencing generic interface (e.g. IQuery<TSelf, TResult>) allows the processor to resolve both the concrete type and the result type at compile time, avoiding reflection and runtime caches.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKSOLID001.md");

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
            var facts = CompilationFacts._create(startContext.Compilation);

            if (!facts._isEnabled)
            {
                return;
            }

            startContext.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                {
                    return;
                }

                if (type.IsAbstract)
                {
                    return;
                }

                var interfaces = type.AllInterfaces;
                _checkGeneric(symbolContext, type, interfaces, facts._query1, facts._query2, "IQuery");
                _checkGeneric(symbolContext, type, interfaces, facts._request1, facts._request2, "IRequest");
                _checkCommand(symbolContext, type, interfaces, facts._command0, facts._command1);
            }, SymbolKind.NamedType);
        });
    }

    private static void _checkGeneric(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> interfaces,
        INamedTypeSymbol? legacyDefinition,
        INamedTypeSymbol? selfDefinition,
        string interfaceName)
    {
        if (legacyDefinition is null || selfDefinition is null)
        {
            return;
        }

        var legacy = interfaces.FirstOrDefault(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, legacyDefinition));
        if (legacy is null)
        {
            return;
        }

        if (legacy.TypeArguments.Length != 1
            || legacy.TypeArguments[0].Kind == SymbolKind.ErrorType)
        {
            return;
        }

        if (_implementsSelf(type, interfaces, selfDefinition))
        {
            return;
        }

        var resultType = legacy.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var selfType = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        _report(context, type, $"{interfaceName}<{selfType}, {resultType}>");
    }

    private static void _checkCommand(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> interfaces,
        INamedTypeSymbol? commandDefinition,
        INamedTypeSymbol? selfDefinition)
    {
        if (commandDefinition is null || selfDefinition is null)
        {
            return;
        }

        if (!interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, commandDefinition)))
        {
            return;
        }

        if (_implementsSelf(type, interfaces, selfDefinition))
        {
            return;
        }

        _report(context, type, $"ICommand<{type.Name}>");
    }

    private static bool _implementsSelf(
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> interfaces,
        INamedTypeSymbol selfDefinition)
    {
        return interfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, selfDefinition)
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type));
    }

    private static void _report(SymbolAnalysisContext context, INamedTypeSymbol type, string suggested)
    {
        var location = type.Locations.FirstOrDefault(static l => l.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(_rule, location, type.Name, suggested));
    }

    private readonly struct CompilationFacts(
        INamedTypeSymbol? query1,
        INamedTypeSymbol? query2,
        INamedTypeSymbol? request1,
        INamedTypeSymbol? request2,
        INamedTypeSymbol? command0,
        INamedTypeSymbol? command1)
    {
        internal readonly INamedTypeSymbol? _query1 = query1;
        internal readonly INamedTypeSymbol? _query2 = query2;
        internal readonly INamedTypeSymbol? _request1 = request1;
        internal readonly INamedTypeSymbol? _request2 = request2;
        internal readonly INamedTypeSymbol? _command0 = command0;
        internal readonly INamedTypeSymbol? _command1 = command1;

        internal bool _isEnabled => _query2 is not null || _request2 is not null || _command1 is not null;

        internal static CompilationFacts _create(Compilation compilation)
        {
            return new CompilationFacts(
                compilation.GetTypeByMetadataName("Ark.Tools.Solid.IQuery`1"),
                compilation.GetTypeByMetadataName("Ark.Tools.Solid.IQuery`2"),
                compilation.GetTypeByMetadataName("Ark.Tools.Solid.IRequest`1"),
                compilation.GetTypeByMetadataName("Ark.Tools.Solid.IRequest`2"),
                compilation.GetTypeByMetadataName("Ark.Tools.Solid.ICommand"),
                compilation.GetTypeByMetadataName("Ark.Tools.Solid.ICommand`1"));
        }
    }
}
