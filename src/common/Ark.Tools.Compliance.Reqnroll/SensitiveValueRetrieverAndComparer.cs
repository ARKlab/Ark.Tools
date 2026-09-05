// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;

using Reqnroll.Assist;

namespace Ark.Tools.Compliance.Reqnroll;

/// <summary>Converts and compares sensitive value objects in Reqnroll tables.</summary>
/// <remarks>
/// Retrieval goes through the generated <see cref="TypeConverter"/>, so a feature table
/// cell is validated and normalized exactly like production input. Comparison accepts the
/// cleartext expectation of the feature file and falls back to the redacted rendering, so a
/// table can assert either form.
/// </remarks>
public sealed class SensitiveValueRetrieverAndComparer : IValueRetriever, IValueComparer
{
    /// <inheritdoc />
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return _isSensitiveValue(propertyType);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Reqnroll resolves table property types dynamically at test runtime.")]
    [UnconditionalSuppressMessage(
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
        return actualValue is not null && _isSensitiveValue(actualValue.GetType());
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Reqnroll resolves table property types dynamically at test runtime.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Reqnroll resolves table property types dynamically at test runtime.")]
    public bool Compare(string expectedValue, object actualValue)
    {
        ArgumentNullException.ThrowIfNull(actualValue);

        if (string.Equals(expectedValue, actualValue.ToString(), StringComparison.Ordinal))
            return true;

        try
        {
            var expected = TypeDescriptor.GetConverter(actualValue.GetType())
                .ConvertFrom(null, CultureInfo.InvariantCulture, expectedValue);
            return Equals(expected, actualValue);
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "Reqnroll resolves table property types dynamically at test runtime.")]
    private static bool _isSensitiveValue(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        foreach (var candidate in type.GetInterfaces())
        {
            if (candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(ISensitiveValue<>)
                && candidate.GenericTypeArguments[0] == type)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Registers sensitive value support with Reqnroll's assist helpers.
/// </summary>
public static class SensitiveValueReqnroll
{
    /// <summary>
    /// Registers the retriever and comparer for every sensitive value object.
    /// </summary>
    public static void Register()
    {
        var retrieverAndComparer = new SensitiveValueRetrieverAndComparer();
        Service.Instance.ValueRetrievers.Register(retrieverAndComparer);
        Service.Instance.ValueComparers.Register(retrieverAndComparer);
    }
}
