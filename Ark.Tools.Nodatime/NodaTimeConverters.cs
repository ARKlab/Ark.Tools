using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public static class NodeTimeConverter
    {
        static NodeTimeConverter()
        {
            TypeDescriptor.AddAttributes(typeof(LocalDate), new System.ComponentModel.TypeConverterAttribute(typeof(LocalDateConverter)));
            TypeDescriptor.AddAttributes(typeof(LocalTime), new System.ComponentModel.TypeConverterAttribute(typeof(LocalTimeConverter)));
            TypeDescriptor.AddAttributes(typeof(LocalDateTime), new System.ComponentModel.TypeConverterAttribute(typeof(LocalDateTimeConverter)));
            TypeDescriptor.AddAttributes(typeof(Instant), new System.ComponentModel.TypeConverterAttribute(typeof(InstantConverter)));
            TypeDescriptor.AddAttributes(typeof(LocalDate?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableLocalDateConverter)));
            TypeDescriptor.AddAttributes(typeof(LocalTime?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableLocalTimeConverter)));
            TypeDescriptor.AddAttributes(typeof(LocalDateTime?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableLocalDateTimeConverter)));
            TypeDescriptor.AddAttributes(typeof(Instant?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableInstantConverter)));
        }

        public static void Register()
        {
            // Register once done using static ctor. this is here just to unsure NodeTimeConverter gets "contructed"
        }
    }
}
