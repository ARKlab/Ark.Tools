// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Core.Analyzers;

/// <summary>Ensures exceptions wrapped in a catch clause preserve the caught exception.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaughtExceptionShouldBeInnerExceptionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _diagnostic = new(
        "ARKCORE005",
        "Preserve the caught exception",
        "The exception thrown from this catch clause must include the caught exception as its inner exception",
        "Exception handling",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When a caught exception is replaced with another exception, preserve it as the inner exception.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzers.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [_diagnostic];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(_analyzeThrow, SyntaxKind.ThrowStatement);
    }

    private static void _analyzeThrow(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;
        if (throwStatement.Expression is not ObjectCreationExpressionSyntax objectCreation)
            return;

        var catchClause = throwStatement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
        if (catchClause is null)
            return;

        var caughtException = catchClause.Declaration?.Identifier;
        if (caughtException is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_diagnostic, objectCreation.GetLocation()));
            return;
        }

        var caughtExceptionSymbol = context.SemanticModel.GetDeclaredSymbol(catchClause.Declaration!, context.CancellationToken);
        if (caughtExceptionSymbol is null)
            return;

        var preservesCaughtException = objectCreation.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(identifier => context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol)
            .Any(symbol => SymbolEqualityComparer.Default.Equals(symbol, caughtExceptionSymbol));

        if (!preservesCaughtException)
            context.ReportDiagnostic(Diagnostic.Create(_diagnostic, objectCreation.GetLocation()));
    }
}
