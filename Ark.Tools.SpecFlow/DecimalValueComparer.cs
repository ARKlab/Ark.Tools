﻿using System.Globalization;

using TechTalk.SpecFlow.Assist;

namespace Ark.Tools.SpecFlow
{
    public class DecimalValueComparer : IValueComparer
    {
        public bool CanCompare(object actualValue)
        {
            return actualValue is decimal || actualValue is decimal?;
        }

        public bool Compare(string expectedValue, object actualValue)
        {
            if (string.IsNullOrWhiteSpace(expectedValue))
                return actualValue == null;

            if (actualValue == null) return false;

            var parsed = decimal.Parse(expectedValue, CultureInfo.CurrentCulture);

            return (decimal)actualValue == parsed;
        }
    }
}
