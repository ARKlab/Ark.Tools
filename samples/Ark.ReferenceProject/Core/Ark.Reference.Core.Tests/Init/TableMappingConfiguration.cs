using Ark.Tools.Reqnroll;

using Reqnroll;

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Reqnroll requires instance methods for BeforeTestRun")]
    public class TableMappingConfiguration
    {
        [BeforeTestRun]
        public static void SupportTableWithNodaTime()
        {
            // last takes prio, this is a last resort
            Reqnroll.Assist.Service.Instance.ValueRetrievers.Register(new StringTypeConverterValueRetriver());

            Reqnroll.Assist.Service.Instance.ValueRetrievers.Register(new NodaTimeValueRetriverAndComparer());
            Reqnroll.Assist.Service.Instance.ValueComparers.Register(new NodaTimeValueRetriverAndComparer());

            Reqnroll.Assist.Service.Instance.ValueRetrievers.Register(new EnumValueRetrieverAndComparer());
            Reqnroll.Assist.Service.Instance.ValueComparers.Register(new EnumValueRetrieverAndComparer());

            Reqnroll.Assist.Service.Instance.ValueRetrievers.Register(new StringValueRetriverAndComparer());
            Reqnroll.Assist.Service.Instance.ValueComparers.Register(new StringValueRetriverAndComparer());

            Reqnroll.Assist.Service.Instance.ValueRetrievers.Register(new NestedJsonRetrieverAndComparer<decimal[]>());
            Reqnroll.Assist.Service.Instance.ValueComparers.Register(new NestedJsonRetrieverAndComparer<decimal[]>());

            Reqnroll.Assist.Service.Instance.ValueRetrievers.Register(new NestedJsonRetrieverAndComparer<decimal?[]>());
            Reqnroll.Assist.Service.Instance.ValueComparers.Register(new NestedJsonRetrieverAndComparer<decimal?[]>());
        }
    }

}