// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Analyzers;

internal sealed class ComplianceCompilationFacts
{
    internal readonly ImmutableArray<INamedTypeSymbol> _classificationAttributes;
    internal readonly ImmutableArray<INamedTypeSymbol> _knownSafeTypes;
    internal readonly bool _telemetryRequiresRegistration;
    internal readonly INamedTypeSymbol? _complianceReviewedAttribute;
    internal readonly INamedTypeSymbol? _notPersonalDataAttribute;
    internal readonly INamedTypeSymbol? _sensitiveValueInterface;
    internal readonly INamedTypeSymbol? _sensitiveValueObjectAttribute;
    internal readonly INamedTypeSymbol? _sqlColumnPolicyAttribute;
    internal readonly INamedTypeSymbol? _sqlDataPolicyAttribute;
    internal readonly INamedTypeSymbol? _vogenValueObjectAttribute;

    private ComplianceCompilationFacts(
        bool telemetryRequiresRegistration,
        INamedTypeSymbol? complianceReviewedAttribute,
        INamedTypeSymbol? notPersonalDataAttribute,
        INamedTypeSymbol? sensitiveValueInterface,
        INamedTypeSymbol? sensitiveValueObjectAttribute,
        INamedTypeSymbol? sqlColumnPolicyAttribute,
        INamedTypeSymbol? sqlDataPolicyAttribute,
        INamedTypeSymbol? vogenValueObjectAttribute,
        ImmutableArray<INamedTypeSymbol> classificationAttributes,
        ImmutableArray<INamedTypeSymbol> knownSafeTypes)
    {
        _classificationAttributes = classificationAttributes;
        _knownSafeTypes = knownSafeTypes;
        _telemetryRequiresRegistration = telemetryRequiresRegistration;
        _complianceReviewedAttribute = complianceReviewedAttribute;
        _notPersonalDataAttribute = notPersonalDataAttribute;
        _sensitiveValueInterface = sensitiveValueInterface;
        _sensitiveValueObjectAttribute = sensitiveValueObjectAttribute;
        _sqlColumnPolicyAttribute = sqlColumnPolicyAttribute;
        _sqlDataPolicyAttribute = sqlDataPolicyAttribute;
        _vogenValueObjectAttribute = vogenValueObjectAttribute;
    }

    internal static ComplianceCompilationFacts _create(Compilation compilation, AnalyzerConfigOptions options)
    {
        var isTestProject = options.TryGetValue("build_property.IsTestProject", out var testProject)
            && string.Equals(testProject, "true", StringComparison.OrdinalIgnoreCase);
        var isHost = compilation.Options.OutputKind
            is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication;

        return new ComplianceCompilationFacts(
            isHost
                && !isTestProject
                && _referencesTelemetry(compilation),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.ComplianceReviewedAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.NotPersonalDataAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.ISensitiveValue`1"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.SensitiveValueObjectAttribute`1"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.Sql.SqlColumnPolicyAttribute"),
            compilation.GetTypeByMetadataName("Ark.Tools.Compliance.Sql.SqlDataPolicyAttribute"),
            compilation.GetTypeByMetadataName("Vogen.ValueObjectAttribute`1"),
            _symbols(
                compilation.GetTypeByMetadataName("Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute"),
                compilation.GetTypeByMetadataName("Ark.Tools.Compliance.PersonalDataAttribute"),
                compilation.GetTypeByMetadataName("Ark.Tools.Compliance.SensitivePersonalDataAttribute"),
                compilation.GetTypeByMetadataName("Ark.Tools.Compliance.UserCredentialsAttribute"),
                compilation.GetTypeByMetadataName("Ark.Tools.Compliance.InfrastructureSecretAttribute"),
                compilation.GetTypeByMetadataName("Ark.Tools.Compliance.PseudonymousAttribute")),
            _symbols(
                compilation.GetTypeByMetadataName("System.Threading.CancellationToken"),
                compilation.GetTypeByMetadataName("System.Threading.CancellationTokenSource")));
    }

    internal static bool _isEnabled(AnalyzerConfigOptions options)
    {
        return !options.TryGetValue("build_property.EnableArkToolsCompliance", out var enabled)
            || !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static ImmutableArray<INamedTypeSymbol> _symbols(params INamedTypeSymbol?[] candidates)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var candidate in candidates)
        {
            if (candidate is not null)
            {
                builder.Add(candidate);
            }
        }

        return builder.ToImmutable();
    }

    private static bool _referencesTelemetry(Compilation compilation)
    {
        foreach (var name in compilation.ReferencedAssemblyNames)
        {
            if (name.Name is "Microsoft.Extensions.Telemetry" or "Microsoft.Extensions.Telemetry.Abstractions")
            {
                return true;
            }
        }

        return false;
    }
}

internal static class ComplianceSymbolFacts
{
    internal static bool _hasAttribute(ISymbol symbol, INamedTypeSymbol? expected)
    {
        if (expected is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (_matchesAttribute(attribute, expected))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool _matchesAttribute(AttributeData attribute, INamedTypeSymbol? expected)
    {
        return expected is not null
            && attribute.AttributeClass is { } attributeClass
            && SymbolEqualityComparer.Default.Equals(attributeClass.OriginalDefinition, expected);
    }

    internal static bool _isClassified(
        ISymbol? symbol,
        ComplianceCompilationFacts facts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_containsSymbol(facts._classificationAttributes, type))
                {
                    return true;
                }
            }
        }

        if (symbol is INamedTypeSymbol named)
        {
            if (facts._sensitiveValueInterface is not null)
            {
                foreach (var candidate in named.AllInterfaces)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (SymbolEqualityComparer.Default.Equals(
                        candidate.OriginalDefinition,
                        facts._sensitiveValueInterface))
                    {
                        return true;
                    }
                }
            }

            return _hasAttribute(named, facts._sensitiveValueObjectAttribute);
        }

        return false;
    }

    internal static ITypeSymbol? _getValueType(ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
    }

    internal static ITypeSymbol _unwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
    }

    internal static bool _isKnownSafeDotNetType(ITypeSymbol type, ComplianceCompilationFacts facts)
    {
        type = _unwrapNullable(type);
        return type is INamedTypeSymbol named && _containsSymbol(facts._knownSafeTypes, named);
    }

    internal static bool _isReviewValid(AttributeData attribute, DateTime today)
    {
        if (attribute.ConstructorArguments.Length < 2
            || attribute.ConstructorArguments[1].Value is not string reason
            || string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "Expires")
            {
                return argument.Value.Value is string expires
                    && DateTime.TryParseExact(expires, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date)
                    && date.Date >= today.Date;
            }
        }

        return true;
    }

    internal static bool _isJustificationMeaningful(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        var normalized = reason!.Trim().Trim('.', '<', '>', '[', ']').ToLowerInvariant();
        return normalized.Length >= 12
            && normalized is not "not personal data" and not "no personal data" and not "not pii"
            && !normalized.StartsWith("todo", StringComparison.Ordinal)
            && !normalized.StartsWith("explain", StringComparison.Ordinal)
            && !normalized.StartsWith("justify", StringComparison.Ordinal)
            && !normalized.StartsWith("add justification", StringComparison.Ordinal)
            && !normalized.StartsWith("your reason", StringComparison.Ordinal);
    }

    /// <summary>
    /// Allocation-free equivalent of <c>type.ToDisplayString() == fullName</c> for top-level,
    /// non-generic types: generic or nested types render with type arguments or the containing
    /// type in their display string, so they can never equal a plain dotted name.
    /// </summary>
    internal static bool _isFullName(INamedTypeSymbol? type, string fullName)
    {
        if (type is null || type.Arity != 0 || type.ContainingType is not null)
        {
            return false;
        }

        var name = type.Name;
        var start = fullName.Length - name.Length;
        if (start <= 0
            || fullName[start - 1] != '.'
            || string.CompareOrdinal(fullName, start, name, 0, name.Length) != 0)
        {
            return false;
        }

        return _isNamespace(type.ContainingNamespace, fullName, start - 1);
    }

    /// <summary>Allocation-free equivalent of <c>ns.ToDisplayString() == dotted</c>.</summary>
    internal static bool _isNamespace(INamespaceSymbol? @namespace, string dotted)
    {
        return _isNamespace(@namespace, dotted, dotted.Length);
    }

    private static bool _isNamespace(INamespaceSymbol? @namespace, string dotted, int end)
    {
        while (@namespace is { IsGlobalNamespace: false })
        {
            var name = @namespace.Name;
            var start = end - name.Length;
            if (start < 0
                || string.CompareOrdinal(dotted, start, name, 0, name.Length) != 0
                || (start > 0 && dotted[start - 1] != '.'))
            {
                return false;
            }

            end = start - 1;
            @namespace = @namespace.ContainingNamespace;
        }

        return end == -1 && @namespace is not null;
    }

    internal static bool _isArkToolsComplianceNamespace(INamespaceSymbol? @namespace)
    {
        return @namespace is
        {
            Name: "Compliance",
            ContainingNamespace:
            {
                Name: "Tools",
                ContainingNamespace:
                {
                    Name: "Ark",
                    ContainingNamespace: { IsGlobalNamespace: true },
                },
            },
        };
    }

    private static bool _containsSymbol(ImmutableArray<INamedTypeSymbol> symbols, INamedTypeSymbol candidate)
    {
        var originalDefinition = candidate.OriginalDefinition;
        foreach (var symbol in symbols)
        {
            if (SymbolEqualityComparer.Default.Equals(originalDefinition, symbol))
            {
                return true;
            }
        }
        return false;
    }
}
