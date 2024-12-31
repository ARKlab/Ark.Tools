using FluentAssertions;

using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

using WebApplicationDemo.Dto;

namespace TestProject
{
    [Binding]
    public class SpecFlowSteps
    {
        private readonly TestClient _client;

        public SpecFlowSteps(TestClient client)
        {
            _client = client;
        }

        [When(@"I get a wrong url")]
        public void WhenIGetAWrongUrl()
        {
            var url = $@"entity/null";
            _client.Get(url);
        }

        [When(@"^I get Entity with id (.*)$")]
        public void WhenIGetEntityWithId(string id)
        {
            _client.Get(["entity", id]);
        }

        [Then("^Content-Type is (.*)$")]
        public void ThenContentTypeIs(string contentType)
        {
            _client.LastResponse.Headers.GetAll("Content-Type").Should().Contain(contentType);
        }


        [Then(@"the Entity has")]
        public void ThenTheEntityHas(Table table)
        {
            var obj = _client.ReadAsMsgPack<Entity.V1.Output>();
            table.CompareToInstance(obj);
        }


    }
}
