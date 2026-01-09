using NodaTime;
using NodaTime.Text;

using Reqnroll;

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    public class Transformations
    {
        [StepArgumentTransformation]
        public OffsetDateTime ParseOffsetDateTime(string isodatetime)
        {
            return OffsetDateTimePattern.ExtendedIso.Parse(isodatetime).GetValueOrThrow();
        }
    }
}