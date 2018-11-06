using System;
using System.Collections.Generic;
using TechTalk.SpecFlow.Assist;

namespace Ark.Tools.SpecFlow
{
    public class EnumValueRetrieverAndComparer : IValueRetriever, IValueComparer
    {
        public object GetValue(string value, Type enumType)
        {
            CheckThatTheValueIsAnEnum(value, enumType);

            return ConvertTheStringToAnEnum(value, enumType);
        }

        public object Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return GetValue(keyValuePair.Value, propertyType);
        }

        public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return _isEnum(propertyType);
        }

        private bool _isEnum(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                return typeof(Enum).IsAssignableFrom(t.GetGenericArguments()[0]);
            return t.IsEnum;
        }

        private static object ConvertTheStringToAnEnum(string value, Type enumType)
        {
            if (!ThisIsNotANullableEnum(enumType) && string.IsNullOrWhiteSpace(value))
                return null;
            var underlyingType = Nullable.GetUnderlyingType(enumType);
            if (underlyingType != null)
                enumType = underlyingType;

            Enum res = null;
            foreach (Enum refValue in Enum.GetValues(enumType))
            {
                if (refValue.ToString() == value)
                {
                    res = refValue;
                    break;
                }
            }

            if (res != null) return res;
            throw GetInvalidOperationException(value);
        }

        private static Type GetTheEnumType(Type enumType)
        {
            return ThisIsNotANullableEnum(enumType) ? enumType : enumType.GetGenericArguments()[0];
        }

        private void CheckThatTheValueIsAnEnum(string value, Type enumType)
        {
            if (ThisIsNotANullableEnum(enumType))
                CheckThatThisNotAnObviouslyIncorrectNonNullableValue(value);

            try
            {
                ConvertTheStringToAnEnum(value, enumType);
            }
            catch
            {
                throw new InvalidOperationException($"No enum with value {value} found");
            }
        }
        private void CheckThatThisNotAnObviouslyIncorrectNonNullableValue(string value)
        {
            if (value == null)
                throw GetInvalidOperationException("{null}");
            if (value == string.Empty)
                throw GetInvalidOperationException("{empty}");
            if (string.IsNullOrWhiteSpace(value))
                throw GetInvalidOperationException("{whitespace}");
        }
        private static bool ThisIsNotANullableEnum(Type enumType)
        {
            return enumType.IsGenericType == false;
        }
        private static InvalidOperationException GetInvalidOperationException(string value) => new InvalidOperationException($"No enum with value {value} found");

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
            var e = ConvertTheStringToAnEnum(expectedValue, actualValue.GetType());
            return e.Equals(actualValue);
        }
    }
}
