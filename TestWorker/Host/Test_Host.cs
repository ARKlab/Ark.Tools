using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestWorker.Dto;
using Ark.Tools.ResourceWatcher.WorkerHost;
using TestWorker.DataProvider;
using TestWorker.Configs;
using TestWorker.Writer;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;
using Microsoft.Extensions.Hosting;
using Ark.Tools.Activity.Provider;
using TestWorker.Constants;
using Ark.Tools.NLog;

namespace TestWorker.Host
{
    public static class Test_Host
    {
        public static IHostBuilder ConfigureFromAppSettings(bool ignoreStateServiceInDev = true, bool useSingleThread = false, Action<ITest_Host_Config> configurationOverrider = null)
        {
            try
            {
                var hostBuilder = new HostBuilder()
                    .AddWorkerHostInfrastracture()
                    .AddApplicationInsightsForWorkerHost()
                    .ConfigureServices((ctx,s) =>
                    {
                        NLogConfigurer.For("Test_Worker")
                            .WithDefaultTargetsAndRulesFromConfiguration("Test_Worker", NLogConfigurer.MailFromDefault, ctx.Configuration)
                            .Apply();
                    })
                    .AddWorkerHost<Host>(s =>
                    {
                        //var config = s.GetService<IConfiguration>();
                        var baseCfg1 = new Test_Host_Config()
                        {
                            StateDbConnectionString = "" //config.GetConnectionString("boh")
                        };


						var rebusCfg = new RebusResourceNotifier_Config()
						{
							AsbConnectionString = Test_Constants.RebusConnString
						};

						configurationOverrider?.Invoke(baseCfg1);

                        var h = new Host(baseCfg1)
                            .WithTestWriter()
                            .WithNotifier(rebusCfg);

                        h.AddProviderFilterConfigurer(c => c.Count = 100);
                        return h;
					})
                    .UseConsoleLifetime();

                return hostBuilder;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        #region Host Class
        public class Host : WorkerHost<Test_File, Test_FileMetadataDto, Test_ProviderFilter>
        {
            public Host(ITest_Host_Config config) : base(config)
            {
                this.UseDataProvider<TestProvider>(deps =>
                {

                });

                this.UseStateProvider<InMemStateProvider>();
            }

            public Task RunOnceAsync(LocalDate date, CancellationToken ctk = default)
            {
                return base.RunOnceAsync(f => f.Date = LocalDate.FromDateTime(DateTime.Today), ctk);
            }

            public Host WithTestWriter()
            {
                this.AppendFileProcessor<TestWriter>(deps =>
                {
                    //deps.Container.RegisterInstance(cfg);
                });

                return this;
            }


			public Host WithNotifier(IRebusResourceNotifier_Config rebusCfg)
			{
				this.AppendFileProcessor<TestWriter_Notifier>(deps =>
				{
					deps.Container.RegisterInstance(rebusCfg);
					deps.Container.RegisterSingleton<RebusResourceNotifier>();
				});

				//_logger.Info("Rebus notifier added to host");

				return this;
			}

		}
        #endregion
    }


}
