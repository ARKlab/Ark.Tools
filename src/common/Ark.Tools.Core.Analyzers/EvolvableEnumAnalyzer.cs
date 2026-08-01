// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Core.Analyzers;

/// <summary>Validates evolvable enum generic arguments at compile time.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EvolvableEnumAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor BackingTypeMismatch = new(
        "ARKCORE001",
        "Evolvable enum backing type mismatch",
        "Backing type '{0}' must exactly match enum '{1}' backing type '{2}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingNotSet = new(
        "ARKCORE002",
        "Evolvable enum requires NOT_SET",
        "Enum '{0}' must declare an explicit NOT_SET = 0 member",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(BackingTypeMismatch, MissingNotSet);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeGenericName, Microsoft.CodeAnalysis.CSharp.SyntaxKind.GenericName);
    }

    private static void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
    {
        var syntax = (GenericNameSyntax)context.Node;
        if (syntax.Identifier.ValueText != "EvolvableEnum")
            return;

        if (context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type is not INamedTypeSymbol wrapper
            || wrapper.ContainingNamespace.ToDisplayString() != "Ark.Tools.Core"
            || wrapper.TypeArguments.Length is < 1 or > 2
            || wrapper.TypeArguments[0] is not INamedTypeSymbol enumType
            || enumType.TypeKind != TypeKind.Enum
            || enumType.EnumUnderlyingType is null)
            return;

        var requestedBacking = wrapper.TypeArguments.Length == 1
            ? context.Compilation.GetSpecialType(SpecialType.System_Int32)
            : wrapper.TypeArguments[1];

        if (!SymbolEqualityComparer.Default.Equals(requestedBacking, enumType.EnumUnderlyingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                BackingTypeMismatch,
                syntax.TypeArgumentList.Arguments[wrapper.TypeArguments.Length - 1].GetLocation(),
                requestedBacking.ToDisplayString(),
                enumType.ToDisplayString(),
                enumType.EnumUnderlyingType.ToDisplayString()));
        }

        var hasNotSet = enumType.GetMembers("NOT_SET")
            .OfType<IFieldSymbol>()
            .Any(field => field.HasConstantValue && IsZero(field.ConstantValue));
        if (!hasNotSet)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingNotSet,
                syntax.TypeArgumentList.Arguments[0].GetLocation(),
                enumType.ToDisplayString()));
        }
    }

    private static bool IsZero(object? value)
        => value is sbyte sb && sb == 0
        || value is byte b && b == 0
        || value is short s && s == 0
        || value is ushort us && us == 0
        || value is int i && i == 0
        || value is uint ui && ui == 0
        || value is long l && l == 0
        || value is ulong ul && ul == 0;
}
