// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
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
                ct => _fixAsync(context.Document, declaration, ct),
                equivalenceKey: SelfGenericInterfaceAnalyzer.DiagnosticId),
            context.Diagnostics);
    }

    private static async Task<Document> _fixAsync(Document document, TypeDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null || root is null || declaration.BaseList is null)
            return document;

        var selfType = _createSelfType(declaration);
        var query1 = semanticModel.Compilation.GetTypeByMetadataName("Ark.Tools.Solid.IQuery`1");
        var request1 = semanticModel.Compilation.GetTypeByMetadataName("Ark.Tools.Solid.IRequest`1");
        var command0 = semanticModel.Compilation.GetTypeByMetadataName("Ark.Tools.Solid.ICommand");

        var replacements = new Dictionary<TypeSyntax, TypeSyntax>();
        foreach (var baseType in declaration.BaseList.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var symbol = semanticModel.GetSymbolInfo(baseType.Type, cancellationToken).Symbol as INamedTypeSymbol;
            if (symbol is null)
                continue;

            var nameSyntax = _getRightmostName(baseType.Type);
            if (nameSyntax is null)
                continue;

            if (SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, query1)
                && nameSyntax is GenericNameSyntax queryName)
            {
                replacements[nameSyntax] = _prependSelf(queryName, selfType);
            }
            else if (SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, request1)
                && nameSyntax is GenericNameSyntax requestName)
            {
                replacements[nameSyntax] = _prependSelf(requestName, selfType);
            }
            else if (SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, command0))
            {
                replacements[nameSyntax] = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("ICommand"),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(selfType)))
                    .WithTriviaFrom(nameSyntax);
            }
        }

        if (replacements.Count == 0)
            return document;

        var newRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        return document.WithSyntaxRoot(newRoot);
    }

    private static TypeSyntax? _getRightmostName(TypeSyntax type)
        => type switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            SimpleNameSyntax simple => simple,
            _ => null,
        };

    private static GenericNameSyntax _prependSelf(GenericNameSyntax name, TypeSyntax selfType)
    {
        var arguments = name.TypeArgumentList.Arguments.Insert(0, selfType);
        return name.WithTypeArgumentList(
            SyntaxFactory.TypeArgumentList(arguments))
            .WithTriviaFrom(name);
    }

    private static TypeSyntax _createSelfType(TypeDeclarationSyntax declaration)
    {
        if (declaration.TypeParameterList is null)
            return SyntaxFactory.IdentifierName(declaration.Identifier);

        return SyntaxFactory.GenericName(
            declaration.Identifier,
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(
                    declaration.TypeParameterList.Parameters
                        .Select(parameter => (TypeSyntax)SyntaxFactory.IdentifierName(parameter.Identifier)))));
    }
}
