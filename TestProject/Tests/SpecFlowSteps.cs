using Microsoft.VisualStudio.TestTools.UnitTesting;
using TechTalk.SpecFlow;

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
			var url = $@"pippo";
			_client.Get(url);
		}
	}
}
