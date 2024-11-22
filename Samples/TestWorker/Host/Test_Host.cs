using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestWorker.Dto;
using Ark.Tools.ResourceWatcher.WorkerHost;
using TestWorker.DataProvider;
using TestWorker.Configs;
using TestWorker.Writer;
using Ark.Tools.Activity.Provider;
using Microsoft.Extensions.Configuration;

namespace TestWorker.HostNs
{
    public static class Test_Host
    {
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

                this.UseSqlStateProvider(config);
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

        public static Host Configure(IConfiguration configuration, Test_Recipe? recipe = null, Action<Test_Host_Config>? configurer = null)
        {
            var localRecipe = configuration["Test:Recipe"];

            Test_Recipe r = default;

            if (recipe.HasValue || Enum.TryParse<Test_Recipe>(localRecipe, out r))
            {
                if (recipe.HasValue)
                    r = recipe.Value;

                Host? h = null;

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

        private static Host _configureForTest(IConfiguration configuration, Action<Test_Host_Config>? configurer)
        {
            var baseCfg = new Test_Host_Config()
            {
                StateDbConnectionString = configuration["ConnectionStrings:Workers.Database"],
                Sleep = TimeSpan.FromSeconds(30),
                MaxRetries = 2,
            };

            //var rebusCfg = new RebusResourceNotifier_Config(configuration["ConnectionStrings:Test.Rebus"])
            //{
            //};

            configurer?.Invoke(baseCfg);

            var h = new Host(baseCfg)
                .WithTestWriter()
                //.WithNotifier(rebusCfg)
                ;

            h.AddProviderFilterConfigurer(c => c.Count = 100);
            return h;
        }
    }


}
