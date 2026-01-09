using Reqnroll.Assist;

using System.Globalization;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Reqnroll(net10.0)', Before:
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

            var parsed = decimal.Parse(expectedValue, CultureInfo.CurrentCulture);

            return (decimal)actualValue == parsed;
        }
=======
namespace Ark.Tools.Reqnroll;

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
>>>>>>> After


namespace Ark.Tools.Reqnroll;

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