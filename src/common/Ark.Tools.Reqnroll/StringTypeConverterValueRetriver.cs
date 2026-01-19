using Reqnroll.Assist;

using System.ComponentModel;

namespace Ark.Tools.Reqnroll;

public class StringTypeConverterValueRetriver : IValueRetriever
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TypeDescriptor used for test data conversion. TypeConverter types must be preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "TypeDescriptor used for test data conversion. TypeConverter types must be preserved.")]
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return !propertyType.IsValueType
            && Nullable.GetUnderlyingType(propertyType) == null
            && TypeDescriptor.GetConverter(propertyType).CanConvertFrom(typeof(string));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TypeDescriptor used for test data conversion. TypeConverter types must be preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "TypeDescriptor used for test data conversion. TypeConverter types must be preserved.")]
    public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return TypeDescriptor.GetConverter(propertyType).ConvertFrom(keyValuePair.Value);
    }
}