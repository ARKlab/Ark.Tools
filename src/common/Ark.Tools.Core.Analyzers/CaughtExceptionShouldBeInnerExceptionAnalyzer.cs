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
        context.RegisterSyntaxNodeAction(_analyzeThrow, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    private static void _analyzeThrow(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = _getObjectCreation(context.Node);
        if (objectCreation is null)
            return;

        var ancestors = context.Node.Ancestors().ToList();
        var catchClause = ancestors.OfType<CatchClauseSyntax>().FirstOrDefault();
        if (catchClause is null
            || ancestors
                .TakeWhile(ancestor => ancestor != catchClause)
                .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            return;

        var caughtExceptionName = catchClause.Declaration?.Identifier.ValueText;
        if (string.IsNullOrEmpty(caughtExceptionName))
            return;

        var caughtExceptionSymbol = context.SemanticModel.GetDeclaredSymbol(catchClause.Declaration!, context.CancellationToken);
        if (caughtExceptionSymbol is null)
            return;

        var preservesCaughtException = objectCreation.ArgumentList?.Arguments
                .SelectMany(argument => argument.DescendantNodesAndSelf())
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol)
                .Any(symbol => SymbolEqualityComparer.Default.Equals(symbol, caughtExceptionSymbol)) ?? false;

        if (!preservesCaughtException)
            context.ReportDiagnostic(Diagnostic.Create(_diagnostic, objectCreation.GetLocation()));
    }

    private static ObjectCreationExpressionSyntax? _getObjectCreation(SyntaxNode node)
    {
        return node switch
        {
            ThrowStatementSyntax statement when statement.Expression is ObjectCreationExpressionSyntax expression => expression,
            ThrowExpressionSyntax expression when expression.Expression is ObjectCreationExpressionSyntax objectCreation => objectCreation,
            _ => null,
        };
    }
}
