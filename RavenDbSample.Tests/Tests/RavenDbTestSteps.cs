using TechTalk.SpecFlow;


namespace RavenDbSample.Tests.Tests
{
	[Binding]
	public class RavenDbTestSteps
	{
		private readonly TestClient _client;

		public RavenDbTestSteps(TestClient client)
		{
			_client = client;
		}

		[When(@"I try to ping")]
		public void WhenITryToPing()
		{
			_client.Get("ping");
		}

		[When(@"I create a new Audit Test")]
		public void WhenICreate()
		{
			var url = $@"AuditTest";

			_client.Get(url);


		}
	}
}
