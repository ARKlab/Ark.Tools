using Reqnroll.Assist;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Reqnroll;

public class StringTypeConverterValueRetriver : IValueRetriever
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This method is used by Reqnroll for test data binding. The propertyType comes from test DTOs that are preserved by the test framework.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067:DynamicallyAccessedMembers",
        Justification = "This method is used by Reqnroll for test data binding. The propertyType comes from test DTOs that are preserved by the test framework.")]
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return !propertyType.IsValueType
            && Nullable.GetUnderlyingType(propertyType) == null
            && TypeDescriptor.GetConverter(propertyType).CanConvertFrom(typeof(string));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This method is used by Reqnroll for test data binding. The propertyType comes from test DTOs that are preserved by the test framework.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067:DynamicallyAccessedMembers",
        Justification = "This method is used by Reqnroll for test data binding. The propertyType comes from test DTOs that are preserved by the test framework.")]
    public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        return TypeDescriptor.GetConverter(propertyType).ConvertFrom(keyValuePair.Value);
    }
}