// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Upgrades an already classified string property to its built-in sensitive value object.</summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(SensitivePropertyRefactoringProvider))]
[Shared]
public sealed class SensitivePropertyRefactoringProvider : CodeRefactoringProvider
{
    /// <inheritdoc />
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var property = root?.FindToken(context.Span.Start).Parent?.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (property is null)
        {
            return;
        }

        // The cheap classification check runs first so unclassified properties never pay the reference search.
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model?.GetDeclaredSymbol(property, context.CancellationToken) is not { } symbol
            || !_isClassified(symbol, model.Compilation)
            || !await ComplianceCodeFixProvider._canConvertAsync(context.Document, property, context.CancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var type = ComplianceCodeFixProvider._sensitiveType(property.Identifier.ValueText)!;
        context.RegisterRefactoring(CodeAction.Create("Use sensitive value object " + type,
            async cancellationToken => await ComplianceCodeFixProvider._convertPropertyAsync(
                context.Document, property, type, cancellationToken).ConfigureAwait(false),
            "UseSensitiveValueObject"));
    }

    private static bool _isClassified(ISymbol symbol, Compilation compilation)
    {
        var classificationAttributes = new[]
        {
            compilation.GetTypeByMetadataName("Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.PersonalDataAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.SensitivePersonalDataAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.UserCredentialsAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.InfrastructureSecretAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.PseudonymousAttribute"),
        };

        foreach (var attribute in symbol.GetAttributes())
        {
            for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
            {
                if (classificationAttributes.Contains(type, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
