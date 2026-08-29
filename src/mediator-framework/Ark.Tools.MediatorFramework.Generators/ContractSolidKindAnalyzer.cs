// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>Reports Mediator Framework contracts that implement more than one Solid kind.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContractSolidKindAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id reported by this analyzer.</summary>
    public const string DiagnosticId = "ARKMF021";

    internal static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Contract has multiple Solid kinds",
        "Contract '{0}' can implement only one of IQuery, IRequest, or ICommand",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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
            if (query1 is null && query2 is null && request1 is null && request2 is null && command0 is null && command1 is null)
                return;

            startContext.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    return;
                if (type.IsAbstract)
                    return;

                var kindCount = 0;
                if (_implements(type, query1, query2))
                    kindCount++;
                if (_implements(type, request1, request2))
                    kindCount++;
                if (_implements(type, command0, command1))
                    kindCount++;
                if (kindCount <= 1)
                    return;

                var location = type.Locations.FirstOrDefault(candidate => candidate.IsInSource);
                if (location is null)
                    return;

                symbolContext.ReportDiagnostic(Diagnostic.Create(_rule, location, type.ToDisplayString()));
            }, SymbolKind.NamedType);
        });
    }

    private static bool _implements(INamedTypeSymbol type, params INamedTypeSymbol?[] definitions)
    {
        return type.AllInterfaces.Any(@interface => definitions.Any(definition =>
            definition is not null
            && (
                SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, definition)
                || SymbolEqualityComparer.Default.Equals(@interface, definition))));
    }
}
