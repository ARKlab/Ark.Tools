using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public class NullableLocalTimeConverter : NullableConverter
    {
        public NullableLocalTimeConverter() : base(typeof(LocalTime?)) { }
    }
}
