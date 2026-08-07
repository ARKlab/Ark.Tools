using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Tests.Auth;
using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Core;

using Flurl.Http;

using NLog;

using System.Diagnostics;

namespace Ark.Reference.Profiling;

internal static class Program
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private const int DefaultWarmupIterations = 10;
    private const int DefaultMeasuredIterations = 100;

    private static async Task Main(string[] args)
    {
        var warmupIterations = GetArgument(args, "--warmup", DefaultWarmupIterations);
        var measuredIterations = GetArgument(args, "--iterations", DefaultMeasuredIterations);

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        TestHost.BeforeTests0();
        TestHost.BeforeTests();

        try
        {
            var client = TestHost.Factory.Get(new Uri("https://localhost:5001"));
            var auth = new AuthTestContext();

            _logger.Info(CultureInfo.InvariantCulture, "Warming up {0} iterations", warmupIterations);
            await RunIterations(client, auth, warmupIterations, false).ConfigureAwait(false);

            _logger.Info(CultureInfo.InvariantCulture, "Running {0} measured iterations", measuredIterations);
            var stopwatch = Stopwatch.StartNew();
            await RunIterations(client, auth, measuredIterations, true).ConfigureAwait(false);
            stopwatch.Stop();

            _logger.Info(CultureInfo.InvariantCulture, "Completed {0} iterations in {1}", measuredIterations, stopwatch.Elapsed);
        }
        finally
        {
            TestHost.AfterTests();
        }
    }

    private static async Task RunIterations(IFlurlClient client, AuthTestContext auth, int iterations, bool measured)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var book = await Send(client, auth, "v1/book")
                .PostJsonAsync(new Book.V1.Create
                {
                    Title = $"Profiling book {iteration}",
                    Author = "Ark.Tools",
                    Genre = BookGenre.Technology,
                    ISBN = $"978-0135957{iteration % 10000:D4}"
                })
                .ReceiveJson<Book.V1.Output>()
                .ConfigureAwait(false);

            await Send(client, auth, $"v1/book/{book.Id}").GetJsonAsync<Book.V1.Output>().ConfigureAwait(false);
            await Send(client, auth, "v1/ping/message")
                .PostJsonAsync(new Ping.V1.Create { Name = $"Profiling ping {iteration}", Type = PingType.Ping1 })
                .ReceiveJson<Ping.V1.Output>()
                .ConfigureAwait(false);

            using var violation = await Send(client, auth, "v1/bookPrintProcess")
                .PostJsonAsync(new BookPrintProcess.V1.Create { BookId = book.Id, ShouldFail = true })
                .ConfigureAwait(false);
            if (!violation.ResponseMessage.IsSuccessStatusCode)
                throw new InvalidOperationException($"Book print process failed with {violation.StatusCode}.");

            using var businessRuleViolation = await Send(client, auth, "v1/bookPrintProcess")
                .PostJsonAsync(new BookPrintProcess.V1.Create { BookId = book.Id, ShouldFail = true })
                .ConfigureAwait(false);
            if (businessRuleViolation.StatusCode != 400)
                throw new InvalidOperationException($"Expected BusinessRuleViolation response 400, got {businessRuleViolation.StatusCode}.");

            using var table = new[] { book }.ToDataTableArk();

            if (measured && iteration % 10 == 0)
                _logger.Info(CultureInfo.InvariantCulture, "Measured iteration {0}", iteration);
        }
    }

    private static IFlurlRequest Send(IFlurlClient client, AuthTestContext auth, string path)
    {
        return auth.SetAuth(client.Request(path));
    }

    private static int GetArgument(string[] args, string name, int defaultValue)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }
}
