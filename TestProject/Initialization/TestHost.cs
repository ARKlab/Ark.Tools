using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using TechTalk.SpecFlow;
using Ark.Tools.Http;
using Flurl.Http;
using Flurl.Http.Configuration;
using System.Net.Http;
using Flurl;
using WebApplicationDemo;
using Microsoft.Extensions.Hosting;

namespace TestProject
{
	[Binding]
	public static class TestHost
	{
		private const string _baseUri = "https://localhost:5001";
		private static IHost _server;
		private static ClientFactory _factory;


		//internal static string SqlConnection;

		[BeforeTestRun]
		public static void BeforeTests()
		{
			Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "SpecFlow");
			Program.InitStatic(Array.Empty<string>());

			//_smtp = SimpleSmtpServer.Start();

			var builder = Program.GetHostBuilder(Array.Empty<string>())
				.ConfigureWebHost(wh =>
				{
					wh.UseTestServer();
				});

			_server = builder.Start();
			_factory = new ClientFactory(_server.GetTestServer());
		}

		[BeforeFeature(Order = 0)]
		public static void BeforeFeature(FeatureContext ctx)
		{
			ctx.Set(_server);
			//ctx.Set(_client);
			//ctx.Set(_smtp);
		}

		[BeforeScenario(Order = 0)]
		public static void BeforeScenario(ScenarioContext ctx)
		{
			ctx.ScenarioContainer.RegisterFactoryAs<IFlurlClient>(c => _factory.Get(_baseUri));
		}

		[AfterScenario]
		public static void FlushLogs()
		{
			try
			{
				LogManager.Flush(TimeSpan.FromSeconds(2));
			}
			catch
			{
			}
		}


		[AfterTestRun]
		public static void AfterTests()
		{
			_server?.Dispose();
			_factory?.Dispose();
			//_smtp?.Dispose();

		}

	}

	class TestServerfactory : DefaultHttpClientFactory
	{
		private readonly TestServer _server;

		public TestServerfactory(TestServer server)
		{
			_server = server;
		}

		public override HttpMessageHandler CreateMessageHandler()
		{
			return _server.CreateHandler();
		}
	}

	class ClientFactory : PerBaseUrlFlurlClientFactory
	{
		private readonly TestServerfactory _server;

		public ClientFactory(TestServer server)
		{
			_server = new TestServerfactory(server);
		}

		protected override IFlurlClient Create(Url url)
		{
			return base.Create(url)
				.ConfigureArkDefaults()
				.Configure(s => s.HttpClientFactory = _server);
		}
	}
}
