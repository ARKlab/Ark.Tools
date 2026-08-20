// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
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
        context.RegisterSyntaxTreeAction(_analyzeTree);
    }

    private static void _analyzeTree(SyntaxTreeAnalysisContext context)
    {
#pragma warning disable MA0045 // SyntaxTreeAnalysisContext does not provide an async callback.
        var root = context.Tree.GetRoot(context.CancellationToken);
#pragma warning restore MA0045
        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
            _analyzeCatch(context, catchClause);
    }

    private static void _analyzeCatch(SyntaxTreeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        var caughtExceptionName = catchClause.Declaration?.Identifier.ValueText;
        if (string.IsNullOrEmpty(caughtExceptionName))
            return;

        foreach (var throwStatement in catchClause.DescendantNodes().OfType<ThrowStatementSyntax>())
        {
            if (throwStatement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault() != catchClause
                || throwStatement.Ancestors()
                .TakeWhile(ancestor => ancestor != catchClause)
                .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                || throwStatement.Expression is not ObjectCreationExpressionSyntax objectCreation)
                continue;

            var preservesCaughtException = objectCreation.ArgumentList?.Arguments
                .SelectMany(argument => argument.DescendantNodesAndSelf())
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.ValueText == caughtExceptionName) == true;

            if (!preservesCaughtException)
                context.ReportDiagnostic(Diagnostic.Create(_diagnostic, objectCreation.GetLocation()));
        }
    }
}
