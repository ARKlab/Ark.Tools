using Reqnroll.Assist;

namespace Ark.Tools.Reqnroll
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

            var parsed = decimal.Parse(expectedValue);

            return (decimal)actualValue == parsed;
        }
    }
}
