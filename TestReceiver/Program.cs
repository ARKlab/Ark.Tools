using Ark.Tools.Activity.Processor;
using SimpleInjector;
using System.Configuration;
using System.Threading;
using NLog;
using Ark.Tools.NLog;
using System;

namespace TestReceiver
{
	class Config : TestReceiver_Config, IRebusSliceActivityManagerConfig
	{
		public string ActivitySqlConnectionString
		{
			get { return "DB"; }
		}

		public string AsbConnectionString
		{
			get { return "Endpoint=sb://ark-playground.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fc3hUuRJJmx/IpQ+89QyYP8VVA6IkwQcToSEt/51+rU="; }
		}

		public string SagaSqlConnectionString
		{
			get { return "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TestPlayground.Database;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True"; }
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			//NLogConfigurer.For("K2E_D_MGP_Prezzi")
			//	.WithDefaultTargetsAndRulesFromAppSettings("K4View_Materializers", "k4view-alerts@ark-energy.eu", "arkive-notifications@ark-energy.eu")
			//	.WithMailRule("Rebus.*", LogLevel.Error)
			//	.Apply();

			NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

			try
			{
				//DapperNodaTimeSetup.Register();
				//set Retry Manager

				var container = new Container();
				var reg = Lifestyle.Singleton.CreateRegistration(typeof(Config), container);
				container.AddRegistration(typeof(TestReceiver_Config), reg);
				container.AddRegistration(typeof(IRebusSliceActivityManagerConfig), reg);

				//container.RegisterSingleton<IDbConnectionManager, SqlConnectionManager>();
				container.RegisterActivities(typeof(RebusSliceActivityManager<>), typeof(TestReceiver_Activity));



				container.StartActivities().GetAwaiter().GetResult();

				Thread.Sleep(Timeout.Infinite);
			}
			catch (Exception ex)
			{
				_logger.Fatal(ex, "The materializer has gone in error");
			}

		}
	}
}
