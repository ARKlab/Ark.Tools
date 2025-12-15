using Reqnroll.Assist;

using System;
using System.Collections.Generic;

namespace Ark.Tools.Reqnroll
{
    public class EnumValueRetrieverAndComparer : IValueRetriever, IValueComparer
    {
        private static object? _getValue(string value, Type enumType)
        {
            EnumValueRetrieverAndComparer._checkThatTheValueIsAnEnum(value, enumType);

            return _convertTheStringToAnEnum(value, enumType);
        }

        public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return _getValue(keyValuePair.Value, propertyType);
        }

        public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return _isEnum(propertyType);
        }

        private static bool _isEnum(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                return t.GetGenericArguments()[0].IsEnum;
            return t.IsEnum;
        }

        private static object? _convertTheStringToAnEnum(string value, Type enumType)
        {
            if (!_thisIsNotANullableEnum(enumType) && string.IsNullOrWhiteSpace(value))
                return null;
            var underlyingType = Nullable.GetUnderlyingType(enumType);
            if (underlyingType != null)
                enumType = underlyingType;

            Enum? res = null;
            foreach (Enum refValue in Enum.GetValues(enumType))
            {
                if (refValue.ToString() == value)
                {
                    res = refValue;
                    break;
                }
            }

            if (res != null) return res;
            throw _getInvalidOperationException(value);
        }

        private static Type _getTheEnumType(Type enumType)
        {
            return _thisIsNotANullableEnum(enumType) ? enumType : enumType.GetGenericArguments()[0];
        }

        private static void _checkThatTheValueIsAnEnum(string value, Type enumType)
        {
            if (_thisIsNotANullableEnum(enumType))
                _checkThatThisNotAnObviouslyIncorrectNonNullableValue(value);

            try
            {
                _convertTheStringToAnEnum(value, enumType);
            }
            catch
            {
                throw new InvalidOperationException($"No enum with value {value} found");
            }
        }
        private static void _checkThatThisNotAnObviouslyIncorrectNonNullableValue(string value)
        {
            if (value == null)
                throw _getInvalidOperationException("{null}");
            if (string.IsNullOrEmpty(value))
                throw _getInvalidOperationException("{empty}");
            if (string.IsNullOrWhiteSpace(value))
                throw _getInvalidOperationException("{whitespace}");
        }
        private static bool _thisIsNotANullableEnum(Type enumType)
        {
            return enumType.IsGenericType == false;
        }
        private static InvalidOperationException _getInvalidOperationException(string value) => new($"No enum with value {value} found");

        public bool CanCompare(object actualValue)
        {
            if (actualValue == null)
                return false;
            else
                return _isEnum(actualValue.GetType());
        }

        public bool Compare(string expectedValue, object actualValue)
        {
            if (string.IsNullOrWhiteSpace(expectedValue) && actualValue == null)
                return true;
            var e = _convertTheStringToAnEnum(expectedValue, actualValue.GetType());
            return e?.Equals(actualValue) == true;
        }
    }
}
