﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

using Reqnroll.Assist;

namespace Ark.Tools.Reqnroll
{
    public class StringTypeConverterValueRetriver : IValueRetriever
    {
        public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return !propertyType.IsValueType
                && Nullable.GetUnderlyingType(propertyType) == null
                && TypeDescriptor.GetConverter(propertyType).CanConvertFrom(typeof(string));
        }

        public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return TypeDescriptor.GetConverter(propertyType).ConvertFrom(keyValuePair.Value);
        }
    }
}
