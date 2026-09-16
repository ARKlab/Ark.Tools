// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
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
        "Use backing type '{2}' for enum '{1}' instead of '{0}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The EvolvableEnum backing type must match the enum's declared underlying type.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKCORE001.md");

    private static readonly DiagnosticDescriptor _missingNotSet = new(
        "ARKCORE002",
        "Evolvable enum requires NOT_SET",
        "Declare an explicit NOT_SET = 0 member in enum '{0}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Evolvable enums require an explicit zero-valued NOT_SET member for forward-compatible defaults.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKCORE002.md");

    private static readonly DiagnosticDescriptor _duplicateName = new(
        "ARKCORE003",
        "Evolvable enum names must be unique",
        "Rename one enum member so evolvable name '{0}' is unique",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Names from enum members and supported naming attributes must be unique.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKCORE003.md");

    private static readonly DiagnosticDescriptor _fullEnum = new(
        "ARKCORE004",
        "Evolvable enum cannot evolve",
        "Leave at least one unused value in enum '{0}' backing type '{1}' for future members",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An evolvable enum with no unused backing values cannot accept future members.",
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKCORE004.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(_backingTypeMismatch, _missingNotSet, _duplicateName, _fullEnum);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var facts = CompilationFacts._create(startContext.Compilation);
            if (!facts._isEnabled)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => _analyzeGenericName(syntaxContext, facts),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.GenericName);
        });
    }

    private static void _analyzeGenericName(
        SyntaxNodeAnalysisContext context,
        CompilationFacts facts)
    {
        var syntax = (GenericNameSyntax)context.Node;
        if (syntax.Identifier.ValueText != "EvolvableEnum")
            return;

        if (context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type is not INamedTypeSymbol wrapper
            || (!SymbolEqualityComparer.Default.Equals(wrapper.OriginalDefinition, facts._evolvableEnum1)
                && !SymbolEqualityComparer.Default.Equals(wrapper.OriginalDefinition, facts._evolvableEnum2))
            || wrapper.TypeArguments.Length is < 1 or > 2
            || wrapper.TypeArguments[0] is not INamedTypeSymbol enumType
            || enumType.TypeKind != TypeKind.Enum
            || enumType.EnumUnderlyingType is null)
            return;

        var fields = enumType.GetMembers().OfType<IFieldSymbol>().ToImmutableArray();
        var requestedBacking = wrapper.TypeArguments.Length == 1
            ? facts._int32Type
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

        var hasNotSet = fields.Any(static field =>
            field.Name == "NOT_SET" && field.HasConstantValue && _isZero(field.ConstantValue));
        if (!hasNotSet)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _missingNotSet,
                enumType.Locations.FirstOrDefault() ?? syntax.TypeArgumentList.Arguments[0].GetLocation(),
                additionalLocations: [syntax.TypeArgumentList.Arguments[0].GetLocation()],
                enumType.ToDisplayString()));
        }

        if (_isFull(enumType, fields))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _fullEnum,
                enumType.Locations.FirstOrDefault() ?? syntax.TypeArgumentList.Arguments[0].GetLocation(),
                enumType.ToDisplayString(),
                enumType.EnumUnderlyingType.ToDisplayString()));
        }

        var names = new Dictionary<string, IFieldSymbol>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            foreach (var name in _getNames(field, facts))
            {
                if (names.TryGetValue(name, out var previous))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        _duplicateName,
                        field.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                        additionalLocations: [previous.Locations.FirstOrDefault() ?? Location.None],
                        messageArgs: [name]));
                }

                names[name] = field;
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

    private static bool _isFull(INamedTypeSymbol enumType, ImmutableArray<IFieldSymbol> fields)
    {
        var values = new HashSet<object>();
        foreach (var field in fields)
        {
            if (field.IsConst && field.HasConstantValue && field.ConstantValue is object value)
                values.Add(value);
        }

        return enumType.EnumUnderlyingType?.SpecialType switch
        {
            SpecialType.System_SByte or SpecialType.System_Byte => values.Count == 256,
            SpecialType.System_Int16 or SpecialType.System_UInt16 => values.Count == 65_536,
            _ => false,
        };
    }

    private static IEnumerable<string> _getNames(IFieldSymbol field, CompilationFacts facts)
    {
        yield return field.Name;
        foreach (var attribute in field.GetAttributes())
        {
            if (_matchesAttribute(attribute, facts._enumMemberAttribute)
                && attribute.NamedArguments.FirstOrDefault(static item => item.Key == "Value").Value.Value is string enumMember)
            {
                yield return enumMember;
            }
            else if (_matchesAttribute(attribute, facts._displayAttribute)
                && attribute.NamedArguments.FirstOrDefault(static item => item.Key == "Name").Value.Value is string display)
            {
                yield return display;
            }
            else if (_matchesAttribute(attribute, facts._displayNameAttribute)
                && attribute.ConstructorArguments.FirstOrDefault().Value is string displayName)
            {
                yield return displayName;
            }
        }
    }

    private static bool _matchesAttribute(AttributeData attribute, INamedTypeSymbol? expected)
    {
        return expected is not null
            && attribute.AttributeClass is { } attributeClass
            && SymbolEqualityComparer.Default.Equals(attributeClass.OriginalDefinition, expected);
    }

    private readonly struct CompilationFacts(
        INamedTypeSymbol? evolvableEnum1,
        INamedTypeSymbol? evolvableEnum2,
        INamedTypeSymbol int32Type,
        INamedTypeSymbol? enumMemberAttribute,
        INamedTypeSymbol? displayAttribute,
        INamedTypeSymbol? displayNameAttribute)
    {
        internal readonly INamedTypeSymbol? _evolvableEnum1 = evolvableEnum1;
        internal readonly INamedTypeSymbol? _evolvableEnum2 = evolvableEnum2;
        internal readonly INamedTypeSymbol _int32Type = int32Type;
        internal readonly INamedTypeSymbol? _enumMemberAttribute = enumMemberAttribute;
        internal readonly INamedTypeSymbol? _displayAttribute = displayAttribute;
        internal readonly INamedTypeSymbol? _displayNameAttribute = displayNameAttribute;

        internal bool _isEnabled => _evolvableEnum1 is not null || _evolvableEnum2 is not null;

        internal static CompilationFacts _create(Compilation compilation)
        {
            return new CompilationFacts(
                compilation.GetTypeByMetadataName("Ark.Tools.Core.EvolvableEnum`1"),
                compilation.GetTypeByMetadataName("Ark.Tools.Core.EvolvableEnum`2"),
                compilation.GetSpecialType(SpecialType.System_Int32),
                compilation.GetTypeByMetadataName("System.Runtime.Serialization.EnumMemberAttribute"),
                compilation.GetTypeByMetadataName("System.ComponentModel.DataAnnotations.DisplayAttribute"),
                compilation.GetTypeByMetadataName("System.ComponentModel.DisplayNameAttribute"));
        }
    }
}
