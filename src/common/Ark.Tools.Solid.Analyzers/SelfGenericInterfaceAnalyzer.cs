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

                _analyzeInterfaces(symbolContext, type, facts);
            }, SymbolKind.NamedType);
        });
    }

    private static void _analyzeInterfaces(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        CompilationFacts facts)
    {
        INamedTypeSymbol? legacyQuery = null;
        INamedTypeSymbol? legacyRequest = null;
        var implementsSelfQuery = false;
        var implementsSelfRequest = false;
        var implementsLegacyCommand = false;
        var implementsSelfCommand = false;

        foreach (var implementedInterface in type.AllInterfaces)
        {
            var originalDefinition = implementedInterface.OriginalDefinition;

            if (legacyQuery is null
                && SymbolEqualityComparer.Default.Equals(originalDefinition, facts._query1))
            {
                legacyQuery = implementedInterface;
            }

            if (legacyRequest is null
                && SymbolEqualityComparer.Default.Equals(originalDefinition, facts._request1))
            {
                legacyRequest = implementedInterface;
            }

            if (SymbolEqualityComparer.Default.Equals(implementedInterface, facts._command0))
            {
                implementsLegacyCommand = true;
            }

            if (implementedInterface.TypeArguments.Length == 0
                || !SymbolEqualityComparer.Default.Equals(implementedInterface.TypeArguments[0], type))
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(originalDefinition, facts._query2))
            {
                implementsSelfQuery = true;
            }

            if (SymbolEqualityComparer.Default.Equals(originalDefinition, facts._request2))
            {
                implementsSelfRequest = true;
            }

            if (SymbolEqualityComparer.Default.Equals(originalDefinition, facts._command1))
            {
                implementsSelfCommand = true;
            }
        }

        if (!implementsSelfQuery)
        {
            _reportGeneric(context, type, legacyQuery, facts._query2, "IQuery");
        }

        if (!implementsSelfRequest)
        {
            _reportGeneric(context, type, legacyRequest, facts._request2, "IRequest");
        }

        if (implementsLegacyCommand && !implementsSelfCommand && facts._command1 is not null)
        {
            _report(context, type, $"ICommand<{type.Name}>");
        }
    }

    private static void _reportGeneric(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol? legacyInterface,
        INamedTypeSymbol? selfDefinition,
        string interfaceName)
    {
        if (legacyInterface is null
            || selfDefinition is null
            || legacyInterface.TypeArguments.Length != 1
            || legacyInterface.TypeArguments[0].Kind == SymbolKind.ErrorType)
        {
            return;
        }

        var resultType = legacyInterface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var selfType = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        _report(context, type, $"{interfaceName}<{selfType}, {resultType}>");
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
