// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Reqnroll.Assist;

using System.ComponentModel;

namespace Ark.MediatorFramework.Sample.Core.Tests.Init;

/// <summary>Binds table cells to Vogen value objects through the <see cref="TypeConverter"/> Vogen generates.</summary>
/// <remarks>
/// Reqnroll's default comparers already compare a value object through its <c>ToString</c>, which Vogen renders as the
/// wrapped value, so only retrieval needs help.
/// </remarks>
internal sealed class ValueObjectValueRetriever : IValueRetriever
{
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return _isValueObject(Nullable.GetUnderlyingType(propertyType) ?? propertyType);
    }

    public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying is not null && string.IsNullOrEmpty(keyValuePair.Value))
            return null;

        return TypeDescriptor.GetConverter(underlying ?? propertyType)
            .ConvertFromString(null, CultureInfo.InvariantCulture, keyValuePair.Value);
    }

    private static bool _isValueObject(Type type)
    {
        return type.GetCustomAttributes(inherit: false)
            .Any(static attribute => attribute.GetType() is { Namespace: "Vogen" } attributeType
                && attributeType.Name.StartsWith("ValueObjectAttribute", StringComparison.Ordinal));
    }
}
