// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.Solid.Analyzers;

/// <summary>
/// Rewrites legacy <c>IQuery&lt;TResult&gt;</c>, <c>IRequest&lt;TResponse&gt;</c> and <c>ICommand</c>
/// base types to their self-referencing generic variants (e.g. <c>IQuery&lt;TSelf, TResult&gt;</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SelfGenericInterfaceCodeFixProvider))]
[Shared]
public sealed class SelfGenericInterfaceCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(SelfGenericInterfaceAnalyzer.DiagnosticId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var declaration = root.FindNode(context.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration?.BaseList is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use self-referencing generic interface for reflection-free dispatch",
                ct => FixAsync(context.Document, declaration, ct),
                equivalenceKey: SelfGenericInterfaceAnalyzer.DiagnosticId),
            context.Diagnostics);
    }

    private static async Task<Document> FixAsync(Document document, TypeDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null || root is null || declaration.BaseList is null)
            return document;

        var selfType = SyntaxFactory.ParseTypeName(
            declaration.Identifier.ValueText + (declaration.TypeParameterList?.ToString() ?? string.Empty));

        var replacements = new Dictionary<TypeSyntax, TypeSyntax>();
        foreach (var baseType in declaration.BaseList.Types)
        {
            var symbol = semanticModel.GetSymbolInfo(baseType.Type, cancellationToken).Symbol as INamedTypeSymbol;
            if (symbol is null || symbol.ContainingNamespace.ToDisplayString() != "Ark.Tools.Solid")
                continue;

            var nameSyntax = GetRightmostName(baseType.Type);
            if (nameSyntax is null)
                continue;

            switch (symbol.Name)
            {
                case "IQuery" when symbol.Arity == 1 && nameSyntax is GenericNameSyntax queryName:
                    replacements[nameSyntax] = PrependSelf(queryName, selfType);
                    break;
                case "IRequest" when symbol.Arity == 1 && nameSyntax is GenericNameSyntax requestName:
                    replacements[nameSyntax] = PrependSelf(requestName, selfType);
                    break;
                case "ICommand" when symbol.Arity == 0:
                    replacements[nameSyntax] = SyntaxFactory.GenericName(
                        SyntaxFactory.Identifier("ICommand"),
                        SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(selfType)))
                        .WithTriviaFrom(nameSyntax);
                    break;
            }
        }

        if (replacements.Count == 0)
            return document;

        var newRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        return document.WithSyntaxRoot(newRoot);
    }

    private static TypeSyntax? GetRightmostName(TypeSyntax type)
        => type switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            SimpleNameSyntax simple => simple,
            _ => null,
        };

    private static GenericNameSyntax PrependSelf(GenericNameSyntax name, TypeSyntax selfType)
    {
        var arguments = new List<TypeSyntax> { selfType };
        arguments.AddRange(name.TypeArgumentList.Arguments);
        return name.WithTypeArgumentList(
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(arguments)))
            .WithTriviaFrom(name);
    }
}
