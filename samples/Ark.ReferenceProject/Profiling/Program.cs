// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Tests.Auth;
using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Core;

using Flurl.Http;

using NLog;

using Microsoft.SqlServer.Dac;

using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Ark.Reference.Profiling;

internal static class Program
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private const int DefaultWarmupIterations = 10;
    private const int DefaultMeasuredIterations = 100;
    private const string DisableDemystifierEnvironmentVariable = "ARK_TOOLS_DISABLE_DEMYSTIFIER";

    private static async Task Main(string[] args)
    {
        var warmupIterations = GetArgument(args, "--warmup", DefaultWarmupIterations);
        var measuredIterations = GetArgument(args, "--iterations", DefaultMeasuredIterations);
        var traceOutput = GetStringArgument(args, "--trace");
        var disableDemystifier = args.Contains("--without-demystifier", StringComparer.Ordinal);
        Process? traceProcess = null;

        Environment.SetEnvironmentVariable(
            DisableDemystifierEnvironmentVariable,
            disableDemystifier ? "1" : null);

        traceOutput = traceOutput is null ? null : Path.GetFullPath(traceOutput);
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        DeployDatabase();
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Core.Database",
            $"{DatabaseUtils.DatabaseConnectionString};Initial Catalog=Ark.Reference.Core.Database");
        TestHost.BeforeTests0();
        TestHost.BeforeTests();

        try
        {
            var client = TestHost.Factory.Get(new Uri("https://localhost:5001"));
            var auth = new AuthTestContext();

            _logger.Info(CultureInfo.InvariantCulture, "Warming up {0} iterations", warmupIterations);
            await RunIterations(client, auth, warmupIterations, false).ConfigureAwait(false);

            if (traceOutput is not null)
                traceProcess = await StartTrace(traceOutput).ConfigureAwait(false);

            _logger.Info(CultureInfo.InvariantCulture, "Running {0} measured iterations", measuredIterations);
            var stopwatch = Stopwatch.StartNew();
            await RunIterations(client, auth, measuredIterations, true).ConfigureAwait(false);
            stopwatch.Stop();

            if (traceProcess is not null)
            {
                await StopTrace(traceProcess).ConfigureAwait(false);
                traceProcess = null;
            }

            _logger.Info(CultureInfo.InvariantCulture, "Completed {0} iterations in {1}", measuredIterations, stopwatch.Elapsed);
        }

        finally
        {
            try
            {
                await StopTraceIfRunning(traceProcess).ConfigureAwait(false);
            }

            finally
            {
                await TestHost.Server.StopAsync().ConfigureAwait(false);
                TestHost.AfterTests();
            }
        }
    }

    private static async Task StopTraceIfRunning(Process? traceProcess)
    {
        if (traceProcess is not null)
            await StopTrace(traceProcess).ConfigureAwait(false);
    }

    private static async Task<Process> StartTrace(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var traceProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet-trace",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ArgumentList =
            {
                "collect",
                "--process-id",
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                "--output",
                outputPath,
                "--profile",
                "dotnet-sampled-thread-time",
                "--providers",
                ProfilingEventSource.ProviderName,
                "--stopping-event-provider-name",
                ProfilingEventSource.ProviderName,
                "--stopping-event-event-name",
                ProfilingEventSource.CaptureCompleteEventName
            }
        }) ?? throw new InvalidOperationException("Failed to start dotnet-trace.");

        var traceReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        traceProcess.OutputDataReceived += (_, eventArgs) => SetTraceReady(traceReady, eventArgs.Data);
        traceProcess.ErrorDataReceived += (_, eventArgs) => SetTraceReady(traceReady, eventArgs.Data);
        traceProcess.BeginOutputReadLine();
        traceProcess.BeginErrorReadLine();

        var traceExit = traceProcess.WaitForExitAsync();
        var completedTask = await Task.WhenAny(traceReady.Task, traceExit).ConfigureAwait(false);
        if (completedTask == traceExit)
            throw new InvalidOperationException($"dotnet-trace exited before capture started with code {traceProcess.ExitCode}.");

        _logger.Info(CultureInfo.InvariantCulture, "Started CPU trace capture at {0}", outputPath);
        return traceProcess;
    }

    private static async Task StopTrace(Process traceProcess)
    {
        ProfilingEventSource.Log.CaptureComplete();
        await traceProcess.WaitForExitAsync().ConfigureAwait(false);
        if (traceProcess.ExitCode != 0)
            throw new InvalidOperationException($"dotnet-trace exited with code {traceProcess.ExitCode}.");

        traceProcess.Dispose();
    }

    private static void SetTraceReady(TaskCompletionSource<bool> traceReady, string? output)
    {
        if (output?.Contains("Output File", StringComparison.Ordinal) == true)
            traceReady.TrySetResult(true);
    }

    private static void DeployDatabase()
    {
        var dacpacPath = Path.Combine(AppContext.BaseDirectory, "Ark.Reference.Core.Database.dacpac");
        using var dacpac = DacPackage.Load(dacpacPath);
        var services = new DacServices(DatabaseUtils.DatabaseConnectionString);
        services.Deploy(
            dacpac,
            "Ark.Reference.Core.Database",
            upgradeExisting: true,
            new DacDeployOptions
            {
                CreateNewDatabase = true,
                AllowIncompatiblePlatform = true
            });
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

            using var printResult = await Send(client, auth, "v1/bookPrintProcess")
                .PostJsonAsync(new BookPrintProcess.V1.Create { BookId = book.Id, ShouldFail = false })
                .ConfigureAwait(false);
            if (!printResult.ResponseMessage.IsSuccessStatusCode)
                throw new InvalidOperationException($"Book print process failed with {printResult.StatusCode}.");

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

    private static string? GetStringArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

[EventSource(Name = "Ark.Reference.Profiling")]
internal sealed class ProfilingEventSource : EventSource
{
    public const string ProviderName = "Ark.Reference.Profiling";
    public const string CaptureCompleteEventName = "CaptureComplete";
    public static readonly ProfilingEventSource Log = new();

    [Event(1, Level = EventLevel.Informational)]
    public void CaptureComplete()
    {
        WriteEvent(1);
    }
}
