using Ark.Tools.SpecFlow;

using TechTalk.SpecFlow;

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    public class TableMappingConfiguration
    {
        [BeforeTestRun]
        public static void SupportTableWithNodaTime()
        {
            // last takes prio, this is a last resort
            TechTalk.SpecFlow.Assist.Service.Instance.ValueRetrievers.Register(new StringTypeConverterValueRetriver());

            TechTalk.SpecFlow.Assist.Service.Instance.ValueRetrievers.Register(new NodaTimeValueRetriverAndComparer());
            TechTalk.SpecFlow.Assist.Service.Instance.ValueComparers.Register(new NodaTimeValueRetriverAndComparer());

            TechTalk.SpecFlow.Assist.Service.Instance.ValueRetrievers.Register(new EnumValueRetrieverAndComparer());
            TechTalk.SpecFlow.Assist.Service.Instance.ValueComparers.Register(new EnumValueRetrieverAndComparer());

            TechTalk.SpecFlow.Assist.Service.Instance.ValueRetrievers.Register(new StringValueRetriverAndComparer());
            TechTalk.SpecFlow.Assist.Service.Instance.ValueComparers.Register(new StringValueRetriverAndComparer());

            TechTalk.SpecFlow.Assist.Service.Instance.ValueRetrievers.Register(new NestedJsonRetrieverAndComparer<decimal[]>());
            TechTalk.SpecFlow.Assist.Service.Instance.ValueComparers.Register(new NestedJsonRetrieverAndComparer<decimal[]>());

            TechTalk.SpecFlow.Assist.Service.Instance.ValueRetrievers.Register(new NestedJsonRetrieverAndComparer<decimal?[]>());
            TechTalk.SpecFlow.Assist.Service.Instance.ValueComparers.Register(new NestedJsonRetrieverAndComparer<decimal?[]>());
        }
    }

}
