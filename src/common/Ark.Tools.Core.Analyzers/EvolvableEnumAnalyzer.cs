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
    private static readonly DiagnosticDescriptor _backingTypeMismatch = new(
        "ARKCORE001",
        "Evolvable enum backing type mismatch",
        "Backing type '{0}' must exactly match enum '{1}' backing type '{2}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The EvolvableEnum backing type must match the enum's declared underlying type.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzers.md");

    private static readonly DiagnosticDescriptor _missingNotSet = new(
        "ARKCORE002",
        "Evolvable enum requires NOT_SET",
        "Enum '{0}' must declare an explicit NOT_SET = 0 member",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Evolvable enums require an explicit zero-valued NOT_SET member for forward-compatible defaults.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzers.md");

    private static readonly DiagnosticDescriptor _duplicateName = new(
        "ARKCORE003",
        "Evolvable enum names must be unique",
        "Evolvable enum name '{0}' is used by multiple enum members",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Names from enum members and supported naming attributes must be unique.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzers.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(_backingTypeMismatch, _missingNotSet, _duplicateName);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var evolvableEnum1 = startContext.Compilation.GetTypeByMetadataName("Ark.Tools.Core.EvolvableEnum`1");
            var evolvableEnum2 = startContext.Compilation.GetTypeByMetadataName("Ark.Tools.Core.EvolvableEnum`2");
            if (evolvableEnum1 is null && evolvableEnum2 is null)
                return;

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => _analyzeGenericName(syntaxContext, evolvableEnum1, evolvableEnum2),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.GenericName);
        });
    }

    private static void _analyzeGenericName(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? evolvableEnum1,
        INamedTypeSymbol? evolvableEnum2)
    {
        var syntax = (GenericNameSyntax)context.Node;
        if (syntax.Identifier.ValueText != "EvolvableEnum")
            return;

        if (context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type is not INamedTypeSymbol wrapper
            || (!SymbolEqualityComparer.Default.Equals(wrapper.OriginalDefinition, evolvableEnum1)
                && !SymbolEqualityComparer.Default.Equals(wrapper.OriginalDefinition, evolvableEnum2))
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
                _backingTypeMismatch,
                syntax.TypeArgumentList.Arguments[wrapper.TypeArguments.Length - 1].GetLocation(),
                additionalLocations: [enumType.Locations.FirstOrDefault() ?? Location.None],
                requestedBacking.ToDisplayString(),
                enumType.ToDisplayString(),
                enumType.EnumUnderlyingType.ToDisplayString()));
        }

        var hasNotSet = enumType.GetMembers("NOT_SET")
            .OfType<IFieldSymbol>()
            .Any(field => field.HasConstantValue && _isZero(field.ConstantValue));
        if (!hasNotSet)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _missingNotSet,
                enumType.Locations.FirstOrDefault() ?? syntax.TypeArgumentList.Arguments[0].GetLocation(),
                additionalLocations: [syntax.TypeArgumentList.Arguments[0].GetLocation()],
                enumType.ToDisplayString()));
        }

        var names = new Dictionary<string, IFieldSymbol>(StringComparer.Ordinal);
        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>().Where(static field => field.HasConstantValue))
        {
            foreach (var name in _getNames(field))
            {
                if (names.TryGetValue(name, out var previous))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        _duplicateName,
                        field.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                        name,
                        additionalLocations: [previous.Locations.FirstOrDefault() ?? Location.None]));
                }
                else
                {
                    names.Add(name, field);
                }
            }
        }
    }

    private static bool _isZero(object? value)
        => value is sbyte sb && sb == 0
        || value is byte b && b == 0
        || value is short s && s == 0
        || value is ushort us && us == 0
        || value is int i && i == 0
        || value is uint ui && ui == 0
        || value is long l && l == 0
        || value is ulong ul && ul == 0;

    private static IEnumerable<string> _getNames(IFieldSymbol field)
    {
        yield return field.Name;
        foreach (var attribute in field.GetAttributes())
        {
            var typeName = attribute.AttributeClass?.ToDisplayString();
            if (typeName == "System.Runtime.Serialization.EnumMemberAttribute"
                && attribute.NamedArguments.FirstOrDefault(item => item.Key == "Value").Value.Value is string enumMember)
                yield return enumMember;
            else if (typeName == "System.ComponentModel.DataAnnotations.DisplayAttribute"
                && attribute.NamedArguments.FirstOrDefault(item => item.Key == "Name").Value.Value is string display)
                yield return display;
            else if (typeName == "System.ComponentModel.DisplayNameAttribute"
                && attribute.ConstructorArguments.FirstOrDefault().Value is string displayName)
                yield return displayName;
        }
    }
}
