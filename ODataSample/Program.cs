using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Raven.Client.Documents;
using Raven.Embedded;
using Microsoft.Extensions.DependencyInjection;

namespace ODataSample
{
    public class Program
    {
		public static void Main(string[] args)
		{
			EmbeddedServer.Instance.StartServer(new ServerOptions
			{
				ServerUrl = "http://127.0.0.1:8080"
			});

			CreateWebHostBuilder(args)
				.ConfigureServices((c, s) => {
					s.AddSingleton<IDocumentStore>(EmbeddedServer.Instance.GetDocumentStore("TestDb"));
					s.AddScoped(ss => ss.GetService<IDocumentStore>().OpenAsyncSession());
				})
				.Build()
				.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
