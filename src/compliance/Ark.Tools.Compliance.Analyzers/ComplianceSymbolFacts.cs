// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Globalization;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.Compliance.Analyzers;

internal static class ComplianceSymbolFacts
{
    internal static bool _hasAttribute(ISymbol symbol, string name)
    {
        return symbol.GetAttributes().Any(attribute => _isAttribute(attribute, name));
    }

    internal static bool _isAttribute(AttributeData attribute, string name)
    {
        var qualifiedName = "Ark.Tools.Compliance." + name;
        return attribute.AttributeClass?.ToDisplayString() == qualifiedName
            || attribute.AttributeClass?.OriginalDefinition.ToDisplayString() == qualifiedName;
    }

    internal static bool _isClassified(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
            {
                if (type.ToDisplayString() == "Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute"
                    || type.ToDisplayString() is "Ark.Tools.Compliance.PersonalDataAttribute"
                        or "Ark.Tools.Compliance.SensitivePersonalDataAttribute"
                        or "Ark.Tools.Compliance.SecretAttribute"
                        or "Ark.Tools.Compliance.PseudonymousAttribute")
                {
                    return true;
                }
            }
        }

        if (symbol is INamedTypeSymbol named)
        {
            return named.AllInterfaces.Any(static candidate =>
                candidate.OriginalDefinition.ToDisplayString() == "Ark.Tools.Compliance.ISensitiveValue<TSelf>")
                || _hasAttribute(named, "SensitiveValueObjectAttribute<T>");
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

    internal static bool _isKnownSafeDotNetType(ITypeSymbol type)
    {
        type = _unwrapNullable(type);
        return type.ToDisplayString() is
            "System.Threading.CancellationToken"
            or "System.Threading.CancellationTokenSource";
    }

    internal static bool _isReviewValid(AttributeData attribute, DateTime today)
    {
        if (attribute.ConstructorArguments.Length < 2
            || attribute.ConstructorArguments[1].Value is not string reason
            || string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        foreach (var argument in attribute.NamedArguments.Where(static argument => argument.Key == "Expires"))
        {
            return argument.Value.Value is string expires
                && DateTime.TryParseExact(expires, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)
                && date.Date >= today.Date;
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
}
