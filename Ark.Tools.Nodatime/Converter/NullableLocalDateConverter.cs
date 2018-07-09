using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public class NullableLocalDateConverter : NullableConverter
    {
        public NullableLocalDateConverter() : base(typeof(LocalDate?)) { }
    }
}
