using Ark.Tools.Http;

using Flurl.Http;
using Flurl.Http.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

using NLog;

using Reqnroll;

using System;
using System.Net.Http;

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize]

namespace WebApplicationDemo.Tests;

[Binding]
public static class TestHost
{
    private static readonly Uri _baseUri = new("https://localhost:5001");
    private static IHost? _server;
    private static ArkFlurlClientFactory? _factory;


    //internal static string SqlConnection;

    [BeforeTestRun]
    public static void BeforeTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
        Program.InitStatic(Array.Empty<string>());

        //_smtp = SimpleSmtpServer.Start();

        var builder = Program.GetHostBuilder(Array.Empty<string>())
            .ConfigureWebHost(wh =>
            {
                wh.UseTestServer();
            });

        _server = builder.Start();
        _factory = new ArkFlurlClientFactory(new TestServerfactory(_server.GetTestServer()));
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
        if (_factory == null) throw new InvalidOperationException("");
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
        //_smtp?.Dispose();

    }

}

sealed class TestServerfactory : DefaultFlurlClientFactory
{
    private readonly TestServer _server;

    public TestServerfactory(TestServer server)
    {
        _server = server;
    }

    public override HttpMessageHandler CreateInnerHandler()
    {
        return _server.CreateHandler();
    }
}