// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

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
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Offers classification declarations, explicit exclusions, and reserved fixture replacements.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ComplianceCodeFixProvider))]
[Shared]
public sealed class ComplianceCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ARKPII001", "ARKPII006");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == "ARKPII006")
            {
                var literal = node.FirstAncestorOrSelf<LiteralExpressionSyntax>();
                if (literal is not null
                    && diagnostic.Properties.TryGetValue("Replacement", out var replacement)
                    && replacement is not null)
                {
                    context.RegisterCodeFix(CodeAction.Create("Replace with reserved test data",
                        async cancellationToken => await _replaceLiteralAsync(context.Document, literal, replacement, cancellationToken).ConfigureAwait(false),
                        "ReservedFixture"), diagnostic);
                }

                continue;
            }

            var declaration = node.AncestorsAndSelf().FirstOrDefault(static candidate =>
                candidate is PropertyDeclarationSyntax or FieldDeclarationSyntax or ParameterSyntax);
            if (declaration is null)
            {
                continue;
            }

            context.RegisterCodeFix(CodeAction.Create("Add [PersonalData]",
                async cancellationToken => await _addAttributeAsync(context.Document, declaration,
                    "global::Ark.Tools.Compliance.PersonalData", null, cancellationToken).ConfigureAwait(false),
                "ClassifyPersonalData"), diagnostic);
            context.RegisterCodeFix(CodeAction.Create("Add [NotPersonalData] justification (review required)",
                async cancellationToken => await _addAttributeAsync(context.Document, declaration,
                    "global::Ark.Tools.Compliance.NotPersonalData",
                    "TODO: explain why this declaration cannot contain personal data", cancellationToken).ConfigureAwait(false),
                "ExplainNotPersonalData"), diagnostic);

            if (declaration is PropertyDeclarationSyntax property
                && await _canConvertAsync(context.Document, property, context.CancellationToken).ConfigureAwait(false))
            {
                var name = _sensitiveType(property.Identifier.ValueText);
                if (name is not null)
                {
                    context.RegisterCodeFix(CodeAction.Create("Use sensitive value object " + name,
                        async cancellationToken => await _convertPropertyAsync(context.Document, property, name, cancellationToken).ConfigureAwait(false),
                        "UseSensitiveValueObject"), diagnostic);
                }
            }
        }
    }

    private static async Task<Document> _addAttributeAsync(
        Document document, SyntaxNode declaration, string name, string? reason, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(name));
        if (reason is not null)
        {
            attribute = attribute.WithArgumentList(SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(reason))))));
        }

        var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
        var stripped = declaration.WithoutLeadingTrivia();
        SyntaxNode updated = stripped switch
        {
            PropertyDeclarationSyntax property => property.AddAttributeLists(list),
            FieldDeclarationSyntax field => field.AddAttributeLists(list),
            ParameterSyntax parameter => parameter.AddAttributeLists(list),
            _ => stripped,
        };
        return document.WithSyntaxRoot(root.ReplaceNode(declaration,
            updated.WithLeadingTrivia(declaration.GetLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation)));
    }

    internal static async Task<bool> _canConvertAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
    {
        if (property.AccessorList is null
            || property.AccessorList.Accessors.Any(static accessor => accessor.Body is not null || accessor.ExpressionBody is not null))
        {
            return false;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return false;
        }

        if (model.GetDeclaredSymbol(property, cancellationToken) is not IPropertySymbol symbol
            || symbol.Type.SpecialType != SpecialType.System_String
            || symbol.IsOverride
            || symbol.ExplicitInterfaceImplementations.Length != 0
            || _sensitiveType(symbol.Name) is not { } typeName
            || model.Compilation.GetTypeByMetadataName("Ark.Tools.Compliance." + typeName) is null)
        {
            return false;
        }

        if (symbol.ContainingType.AllInterfaces.SelectMany(static type => type.GetMembers())
            .Any(member => SymbolEqualityComparer.Default.Equals(
                symbol.ContainingType.FindImplementationForInterfaceMember(member), symbol)))
        {
            return false;
        }

        if (property.Initializer is { } initializer
            && !model.GetConstantValue(initializer.Value, cancellationToken).HasValue
            && initializer.Value is not DefaultExpressionSyntax
            && !initializer.Value.IsKind(SyntaxKind.DefaultLiteralExpression)
            && initializer.Value is not PostfixUnaryExpressionSyntax
            {
                Operand: LiteralExpressionSyntax or DefaultExpressionSyntax,
                RawKind: (int)SyntaxKind.SuppressNullableWarningExpression,
            })
        {
            return false;
        }

        // Do not silently break assignments or change how existing callers observe a string.
        var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
        return !references.SelectMany(static reference => reference.Locations).Any();
    }

    internal static async Task<Document> _convertPropertyAsync(
        Document document, PropertyDeclarationSyntax property, string name, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var qualified = "global::Ark.Tools.Compliance." + name;
        var nullable = property.Type is NullableTypeSyntax;
        var updated = property.WithType(SyntaxFactory.ParseTypeName(qualified + (nullable ? "?" : string.Empty))
            .WithTriviaFrom(property.Type));
        if (property.Initializer is { Value: var value })
        {
            if (value is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed)
            {
                value = suppressed.Operand;
            }

            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var constant = model is null ? default : model.GetConstantValue(value, cancellationToken);
            ExpressionSyntax initializer = value.IsKind(SyntaxKind.DefaultLiteralExpression) || value is DefaultExpressionSyntax
                || constant is { HasValue: true, Value: null })
                ? SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)
                : SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression(qualified + ".From"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(value))));

            updated = updated.WithInitializer(property.Initializer.WithValue(
                initializer.WithTriviaFrom(property.Initializer.Value)));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(property, updated.WithAdditionalAnnotations(Formatter.Annotation)));
    }

    internal static string? _sensitiveType(string name)
    {
        return name.TrimStart('_').ToLowerInvariant() switch
        {
            "email" or "emailaddress" => "EmailAddress",
            "phone" or "phonenumber" or "mobilenumber" => "PhoneNumber",
            "firstname" or "lastname" or "fullname" or "personname" => "PersonName",
            "postaladdress" or "homeaddress" => "PostalAddressLine",
            "ssn" or "nationalidentifier" or "nationalidentificationnumber" or "taxcode" => "NationalIdentifier",
            "apikey" => "ApiKey",
            "iban" => "Iban",
            "bearertoken" => "BearerToken",
            _ => null,
        };
    }

    private static async Task<Document> _replaceLiteralAsync(
        Document document, LiteralExpressionSyntax literal, string replacement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var updated = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(replacement)).WithTriviaFrom(literal);
        return document.WithSyntaxRoot(root.ReplaceNode(literal, updated));
    }
}
