using Ark.Tools.Activity.Processor;
using SimpleInjector;
using System.Threading;
using System;
using System.Threading.Tasks;

namespace TestReceiver
{
    sealed class Config : TestReceiver_Config, IRebusSliceActivityManagerConfig
	{
		public string? RebusConnstring { get; set; }
		public string ActivitySqlConnectionString
		{
			get { return "DB"; }
		}

		public string AsbConnectionString => RebusConnstring ?? throw new InvalidOperationException("");

		public string SagaSqlConnectionString
		{
			get { return "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TestPlayground.Database;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True"; }
		}
	}


	sealed class Program
	{

		static async Task Main(string[] args)
		{
			NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

			try
			{
                await using var container = new Container();

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



                await container.StartActivities();
                await Task.Delay(Timeout.Infinite);
			}
			catch (Exception ex)
			{
				_logger.Fatal(ex, "The materializer has gone in error");
			}

		}
	}
}
