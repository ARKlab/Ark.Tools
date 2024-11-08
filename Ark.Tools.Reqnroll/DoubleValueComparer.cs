using System;

using Reqnroll.Assist;

namespace Ark.Tools.Reqnroll
{
    public class DoubleValueComparer : IValueComparer
    {
        public bool CanCompare(object actualValue)
        {
            return actualValue is double || actualValue is double?;
        }

        public bool Compare(string expectedValue, object actualValue)
        {
            if (string.IsNullOrWhiteSpace(expectedValue))
                return actualValue == null;

            if (actualValue == null) return false;

            var parsed = double.Parse(expectedValue);

            return _aboutEqual((double)actualValue, parsed);
        }

        private static bool _aboutEqual(double x, double y)
        {
            double epsilon = Math.Max(Math.Abs(x), Math.Abs(y)) * 1E-15;
            return Math.Abs(x - y) <= epsilon;
        }
    }
}
