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

    private static readonly DiagnosticDescriptor _missingDeclarationDiagnostic = new(
        "ARKCORE006",
        "Capture the caught exception",
        "The catch clause must capture the exception before throwing a replacement",
        "Exception handling",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When a caught exception is replaced with another exception, capture it so it can be preserved as the inner exception.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzers.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [_diagnostic, _missingDeclarationDiagnostic];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(_analyzeThrow, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    private static void _analyzeThrow(SyntaxNodeAnalysisContext context)
    {
        CatchClauseSyntax? catchClause = null;
        foreach (var ancestor in context.Node.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                return;
            if (ancestor is CatchClauseSyntax candidate)
            {
                catchClause = candidate;
                break;
            }
        }

        if (catchClause is null)
            return;

        if (context.Node is ThrowStatementSyntax { Expression: null })
            return;

        if (catchClause.Declaration is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_missingDeclarationDiagnostic, context.Node.GetLocation()));
            return;
        }

        var caughtExceptionSymbol = context.SemanticModel.GetDeclaredSymbol(catchClause.Declaration, context.CancellationToken);
        if (caughtExceptionSymbol is null)
            return;

        var expression = context.Node switch
        {
            ThrowStatementSyntax statement => statement.Expression,
            ThrowExpressionSyntax throwExpression => throwExpression.Expression,
            _ => null,
        };

        if (expression is IdentifierNameSyntax identifier
            && SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                caughtExceptionSymbol))
            return;

        var objectCreation = _getObjectCreation(expression);
        var preservesCaughtException = objectCreation?.ArgumentList?.Arguments
            .Any(argument => argument.Expression is IdentifierNameSyntax identifier
                && SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                    caughtExceptionSymbol)) ?? false;

        if (!preservesCaughtException)
            context.ReportDiagnostic(Diagnostic.Create(_diagnostic, context.Node.GetLocation()));
    }

    private static ObjectCreationExpressionSyntax? _getObjectCreation(ExpressionSyntax? expression)
    {
        return expression as ObjectCreationExpressionSyntax;
    }
}
