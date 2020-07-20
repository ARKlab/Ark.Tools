using Ark.Tools.Activity.Processor;
using SimpleInjector;
using System.Configuration;
using System.Threading;
using NLog;
using Ark.Tools.NLog;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace TestReceiver
{
	class Config : TestReceiver_Config, IRebusSliceActivityManagerConfig
	{
		public string RebusConnstring { get; set; }
		public string ActivitySqlConnectionString
		{
			get { return "DB"; }
		}

		public string AsbConnectionString => RebusConnstring;

		public string SagaSqlConnectionString
		{
			get { return "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TestPlayground.Database;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True"; }
		}
	}


	class Program
	{

		static void Main(string[] args)
		{
			NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

			try
			{
				var container = new Container();

				var cfg = new Config()
				{
					RebusConnstring = "12"
				};


				var reg = Lifestyle.Singleton.CreateRegistration(typeof(Config), container);


				container.RegisterInstance(cfg);
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
