using Reqnroll.Assist;

using System.ComponentModel;

namespace Ark.Tools.Reqnroll;

public class StringTypeConverterValueRetriver : IValueRetriever
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Fallback to GetConverter for types not registered via TypeDescriptor.RegisterType. This is a test utility where TypeConverter types are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Fallback to GetConverter for types not registered via TypeDescriptor.RegisterType. This is a test utility where TypeConverter types are preserved.")]
    public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        if (propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null)
            return false;

        TypeConverter? converter = null;
        
        try
        {
            // Try registered types first (trim-safe)
            converter = TypeDescriptor.GetConverterFromRegisteredType(propertyType);
        }
        catch (InvalidOperationException)
        {
            // Type not registered - fallback to reflection-based lookup
            converter = TypeDescriptor.GetConverter(propertyType);
        }
        
        return converter?.CanConvertFrom(typeof(string)) == true;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Fallback to GetConverter for types not registered via TypeDescriptor.RegisterType. This is a test utility where TypeConverter types are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Fallback to GetConverter for types not registered via TypeDescriptor.RegisterType. This is a test utility where TypeConverter types are preserved.")]
    public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
    {
        TypeConverter? converter = null;
        
        try
        {
            // Try registered types first (trim-safe)
            converter = TypeDescriptor.GetConverterFromRegisteredType(propertyType);
        }
        catch (InvalidOperationException)
        {
            // Type not registered - fallback to reflection-based lookup
            converter = TypeDescriptor.GetConverter(propertyType);
        }
        
        return converter?.ConvertFrom(keyValuePair.Value);
    }
}