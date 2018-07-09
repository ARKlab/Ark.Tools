using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public class NullableInstantConverter : NullableConverter
    {
        public NullableInstantConverter() : base(typeof(Instant?)) { }
    }
}
