// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using Reqnroll.Assist;

using System.ComponentModel;

namespace Ark.Tools.Reqnroll;

/// <summary>Converts and compares evolvable-enum values in Reqnroll tables.</summary>
public sealed class EvolvableEnumValueRetrieverAndComparer : IValueRetriever, IValueComparer
{
    /// <inheritdoc />
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return IsEvolvableEnum(propertyType);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Reqnroll resolves table property types dynamically at test runtime.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "Reqnroll resolves table property types dynamically at test runtime.")]
    public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (string.IsNullOrWhiteSpace(keyValuePair.Value) && type != propertyType)
            return null;

        return TypeDescriptor.GetConverter(type).ConvertFrom(null, CultureInfo.InvariantCulture, keyValuePair.Value);
    }

    /// <inheritdoc />
    public bool CanCompare(object actualValue)
    {
        return IsEvolvableEnum(actualValue.GetType());
    }

    /// <inheritdoc />
    public bool Compare(string expectedValue, object actualValue)
    {
        return string.Equals(expectedValue, actualValue.ToString(), StringComparison.Ordinal);
    }

    private static bool IsEvolvableEnum(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(EvolvableEnum<>)
                || type.GetGenericTypeDefinition() == typeof(EvolvableEnum<,>));
    }
}
