using NodaTime;
using NodaTime.Text;
using TechTalk.SpecFlow;

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
