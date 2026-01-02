using Ark.Tools.Activity.Processor;

using SimpleInjector;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestReceiver
{
    sealed class Config : ITestReceiver_Config, IRebusSliceActivityManagerConfig
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


                var reg = Lifestyle.Singleton.CreateRegistration<Config>(container);


                container.RegisterInstance(cfg);
                container.AddRegistration<ITestReceiver_Config>(reg);
                container.AddRegistration<IRebusSliceActivityManagerConfig>(reg);

                //container.RegisterSingleton<IDbConnectionManager, SqlConnectionManager>();
                container.RegisterActivities(typeof(RebusSliceActivityManager<>), typeof(TestReceiver_Activity));



                await container.StartActivities().ConfigureAwait(false);
                await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "The materializer has gone in error");
            }

        }
    }
}