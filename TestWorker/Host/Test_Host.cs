﻿using NodaTime;
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
using Microsoft.Extensions.Configuration;

namespace TestWorker.HostNs
{
    public static class Test_Host
    {
     //   public static IHostBuilder ConfigureFromAppSettings(bool ignoreStateServiceInDev = true, bool useSingleThread = false, Action<ITest_Host_Config> configurationOverrider = null)
     //   {
     //       try
     //       {
     //           var hostBuilder = new HostBuilder()
     //               .AddWorkerHostInfrastracture()
     //               .AddApplicationInsightsForWorkerHost()
     //               .ConfigureServices((ctx,s) =>
     //               {
     //                   NLogConfigurer.For("Test_Worker")
     //                       .WithDefaultTargetsAndRulesFromConfiguration("Test_Worker", NLogConfigurer.MailFromDefault, ctx.Configuration)
     //                       .Apply();
     //               })
     //               .AddWorkerHost<Host>(s =>
     //               {
     //                   //var config = s.GetService<IConfiguration>();
     //                   var baseCfg1 = new Test_Host_Config()
     //                   {
     //                       StateDbConnectionString = "" //config.GetConnectionString("boh")
     //                   };


					//	var rebusCfg = new RebusResourceNotifier_Config()
					//	{
					//		AsbConnectionString = Test_Constants.RebusConnString
					//	};

					//	configurationOverrider?.Invoke(baseCfg1);

     //                   var h = new Host(baseCfg1)
     //                       .WithTestWriter()
     //                       .WithNotifier(rebusCfg);

     //                   h.AddProviderFilterConfigurer(c => c.Count = 100);
     //                   return h;
					//})
     //               .UseConsoleLifetime();

     //           return hostBuilder;
     //       }
     //       catch (Exception e)
     //       {
     //           throw e;
     //       }
     //   }

        public class Host : WorkerHost<Test_File, Test_FileMetadataDto, Test_ProviderFilter>
        {
            public ITest_Host_Config Config { get; private set; }

            public Host(ITest_Host_Config config) : base(config)
            {
                Config = config;

                this.UseDataProvider<TestProvider>(deps =>
                {
                    deps.Container.RegisterInstance(config);
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

        public static Host Configure(IConfiguration configuration, Test_Recipe? recipe = null, Action<ITest_Host_Config> configurer = null)
        {
            var mailingList = configuration["NLog:MailingList"];
            var nlogConnectionString = configuration["ConnectionStrings:NLog.Database"];
            var nlogSmtp = configuration["ConnectionStrings:NLog.Smtp"];
            var localRecipe = configuration["Test:Recipe"];

            var ns = Test_Constants.AppName;

            Test_Recipe r = default(Test_Recipe);

            if (recipe.HasValue || Enum.TryParse<Test_Recipe>(localRecipe, out r))
            {
                if (recipe.HasValue)
                    r = recipe.Value;

                NLogConfigurer
                    .For(ns)
                    .WithDefaultTargetsAndRules(logTableName: $"{r}_{ns.Replace(".", "")}", nlogConnectionString, NLogConfigurer.MailFromDefault, mailingList, nlogSmtp)
                    .Apply();

                Host h = null;

                switch (r)
                {
                    case Test_Recipe.Test:
                        h = _configureForTest(configuration, configurer);
                        break;
                    default:
                        throw new NotSupportedException($"Recipe not supported: {recipe}");
                }

                return h;
            }

            throw new InvalidOperationException("Invalid Recipe");
        }

        private static Host _configureForTest(IConfiguration configuration, Action<ITest_Host_Config> configurer)
        {
            var baseCfg = new Test_Host_Config()
            {
                StateDbConnectionString = configuration["ConnectionStrings:KTM.Database"],
            };

            var rebusCfg = new RebusResourceNotifier_Config()
            {
                AsbConnectionString = configuration["ConnectionStrings:Test.Rebus"]
            };

            configurer?.Invoke(baseCfg);

            var h = new Host(baseCfg)
                .WithTestWriter()
                .WithNotifier(rebusCfg);

            h.AddProviderFilterConfigurer(c => c.Count = 100);
            return h;
        }
    }


}
