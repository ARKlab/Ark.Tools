using Reqnroll.Assist;

using System;
using System.Collections.Generic;

namespace Ark.Tools.Reqnroll
{
    public class StringValueRetriverAndComparer : IValueRetriever, IValueComparer
    {
        public bool CanCompare(object actualValue)
        {
            return actualValue is string;
        }

        public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return propertyType == typeof(string);
        }

        public bool Compare(string expectedValue, object actualValue)
        {
            var target = actualValue as string;
            var t = string.IsNullOrEmpty(target) ? null : target;
            var e = string.IsNullOrEmpty(expectedValue) ? null : expectedValue;

            return t == e;
        }

        public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return string.IsNullOrEmpty(keyValuePair.Value) ? null : keyValuePair.Value;
        }
    }
}