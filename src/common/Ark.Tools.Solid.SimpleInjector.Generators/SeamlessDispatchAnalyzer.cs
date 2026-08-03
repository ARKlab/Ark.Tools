// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.Solid.SimpleInjector.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SeamlessDispatchAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SOLID001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use automatic Solid dispatch",
        "The generated Solid dispatcher is registered automatically; replace '{0}.Create' with the normal constructor",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Create",
                Expression: IdentifierNameSyntax processor
            }
            || processor.Identifier.ValueText is not ("SimpleInjectorRequestProcessor"
                or "SimpleInjectorQueryProcessor"
                or "SimpleInjectorCommandProcessor"))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), processor.Identifier.ValueText));
    }
}
