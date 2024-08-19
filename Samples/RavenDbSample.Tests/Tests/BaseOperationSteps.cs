using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenDbSample.Models;
using TechTalk.SpecFlow;

namespace RavenDbSample.Tests.Tests
{
	[Binding]
	public class BaseOperationSteps
	{
		private readonly TestClient _client;
		private BaseOperation _baseOperation;

		public BaseOperationSteps(TestClient client)
		{
			_client = client;
		}

		[When(@"I create a new BaseOperation")]
		public void ICreateANewBaseOperation()
		{
			var url = $@"BaseOperations";

			var op = new BaseOperation()
			{
				Id = "Pippo",
				B = new B()
				{
					Id = "SpecB-1"
				}

			};

			_client.PostAsJson(url, op);
		}
		[When(@"I get the BaseOperation with Id '(.*)'")]
		public void WhenIGetTheBaseOperationWithId(string id)
		{
			var url = $@"BaseOperations/{id}";
			_client.Get(url);
		}

		[When(@"I read the BaseOperation")]
		public void IReadTheBaseOperation()
		{
			_baseOperation = _client.ReadAs<BaseOperation>();
		}

		[Then(@"The BaseOperation has B Id equal to '(.*)'")]
		public void ThenTheLastRevisionHasStatus(string value)
		{
			Assert.AreEqual(value, _baseOperation.B.Id);
		}



	}
}
