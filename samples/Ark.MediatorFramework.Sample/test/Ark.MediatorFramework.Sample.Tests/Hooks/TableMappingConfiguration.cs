// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Reqnroll;
using Ark.Tools.Core;

using Reqnroll;
using Reqnroll.Assist;

using System.ComponentModel;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Registers the table mappings used by the sample's contract scenarios.</summary>
[Binding]
public sealed class TableMappingConfiguration
{
    /// <summary>Registers enum conversion and comparison once for the test run.</summary>
    [BeforeTestRun]
    public static void RegisterMappings()
    {
        Service.Instance.ValueRetrievers.Register(new EnumValueRetrieverAndComparer());
        Service.Instance.ValueComparers.Register(new EnumValueRetrieverAndComparer());
        Service.Instance.ValueRetrievers.Register(new EvolvableEnumValueRetrieverAndComparer());
        Service.Instance.ValueComparers.Register(new EvolvableEnumValueRetrieverAndComparer());
    }
}

internal sealed class EvolvableEnumValueRetrieverAndComparer : IValueRetriever, IValueComparer
{
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return IsEvolvableEnum(propertyType);
    }

    public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (string.IsNullOrWhiteSpace(keyValuePair.Value) && type != propertyType)
            return null;

        return TypeDescriptor.GetConverter(type).ConvertFrom(null, CultureInfo.InvariantCulture, keyValuePair.Value);
    }

    public bool CanCompare(object actualValue)
    {
        return IsEvolvableEnum(actualValue.GetType());
    }

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
