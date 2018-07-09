using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public class NullableLocalDateTimeConverter : NullableConverter
    {
        public NullableLocalDateTimeConverter() : base(typeof(LocalDateTime?)) { }
    }
}
