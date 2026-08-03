// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Solid.SimpleInjector.Generators;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SeamlessDispatchCodeFixProvider)), Shared]
public sealed class SeamlessDispatchCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SeamlessDispatchAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        context.RegisterCodeFix(
            Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                "Use the automatic dispatcher",
                cancellationToken => ReplaceCreateAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                equivalenceKey: "UseAutomaticDispatcher"),
            diagnostic);
        await Task.CompletedTask;
    }

    private static async Task<Document> ReplaceCreateAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(span) as InvocationExpressionSyntax;
        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(
            invocation,
            SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(memberAccess.Expression.ToString()))
                .WithArgumentList(SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(invocation.ArgumentList.Arguments[0])))
                .WithTriviaFrom(invocation));
        return editor.GetChangedDocument();
    }
}
