// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;

using AwesomeAssertions;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests;

[TestClass]
public sealed class AzureFunctionsBoundaryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task HealthEndpointIsDiscoveredByCoreTools()
    {
        await using var host = await FunctionHost.StartAsync(TestContext.CancellationToken).ConfigureAwait(false);
        using var client = new HttpClient
        {
            BaseAddress = host.BaseAddress,
        };

        using var response = await client.GetAsync(
            new Uri("healthCheck", UriKind.Relative),
            TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public void SelectedApplicationEndpointsMatchTheParityMatrix()
    {
        var hostMarker = typeof(Ark.MediatorFramework.Sample.AzureFunctions.Program).Assembly
            .GetCustomAttributes<HttpHostAttribute>()
            .Single();
        var excluded = hostMarker.ExcludedContracts.ToHashSet(EqualityComparer<Type>.Default);
        var actual = hostMarker.ContractAssemblyMarker.Assembly
            .GetTypes()
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<HttpEndpointAttribute>()))
            .Where(item => item.Attribute is not null && !excluded.Contains(item.Type))
            .Select(item => new EndpointRow(
                item.Type.Name,
                item.Attribute!.Verb,
                item.Attribute.Template))
            .OrderBy(row => row.TypeName, StringComparer.Ordinal)
            .ToArray();

        actual.Should().Equal(ExpectedEndpoints.OrderBy(row => row.TypeName, StringComparer.Ordinal));
    }

    private static readonly EndpointRow[] ExpectedEndpoints =
    [
        new("GetAuditsQuery", "GET", "/api/v{version}/audits"),
        new("ComposeGreetingRequest", "POST", "/api/v{version}/greetings/compose"),
        new("GetDocumentQuery", "GET", "/api/v{version}/greeting-cards/{id}/download"),
        new("UploadGreetingCardRequest", "POST", "/api/v{version}/greeting-cards/{id}"),
        new("UploadGreetingCardsRequest", "POST", "/api/v{version}/greeting-cards/{id}/batch"),
        new("GetGreetingQuery", "GET", "/greetings/{id}"),
        new("GetGreetingV2Query", "GET", "/api/v{version}/greetings-v2/{id}"),
        new("GetGreetingsStreamQuery", "GET", "/api/v{version}/greetings/stream"),
        new("UpdateGreetingRequest", "POST", "/api/v{version}/greetings/{id}/envelope"),
        new("RefreshGreetingCommand", "POST", "/api/v{version}/greetings/refresh"),
        new("SearchGreetingsQuery", "GET", "/api/v{version}/greetings"),
        new("UpdateGreetingMessageRequest", "PUT", "/api/v{version}/greetings/{id}"),
    ];

    private sealed record EndpointRow(string TypeName, string Verb, string Route);

    private sealed class FunctionHost : IAsyncDisposable
    {
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
        private static readonly Regex SecretPattern = new(
            "(?i)(authorization\\s*:\\s*|connectionstring\\s*[=:]\\s*)[^\\s,;]+",
            RegexOptions.Compiled
                | RegexOptions.CultureInvariant
                | RegexOptions.ExplicitCapture
                | RegexOptions.NonBacktracking);
        private readonly Process _process;
        private readonly StreamWriter _log;
        private readonly string _logPath;
        private readonly Channel<string> _logLines;
        private readonly Task _logPumpTask;

        private FunctionHost(
            Process process,
            StreamWriter log,
            string logPath,
            Channel<string> logLines,
            Task logPumpTask,
            Uri baseAddress)
        {
            _process = process;
            _log = log;
            _logPath = logPath;
            _logLines = logLines;
            _logPumpTask = logPumpTask;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public static async Task<FunctionHost> StartAsync(CancellationToken cancellationToken)
        {
            var appDirectory = Environment.GetEnvironmentVariable("ARK_AZF_FUNCTION_APP_DIR")
                ?? FindFunctionAppDirectory();
            var port = GetAvailablePort();
            var logPath = Path.Combine(Path.GetTempPath(), $"ark-azf-{Guid.NewGuid():N}.log");
            var log = new StreamWriter(logPath) { AutoFlush = true };
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "func",
                    Arguments = $"start --port {port} --dotnet-isolated --verbose",
                    WorkingDirectory = appDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
            process.StartInfo.Environment["AzureWebJobsScriptRoot"] = appDirectory;
            process.StartInfo.Environment["AzureWebJobsStorage"] = "UseDevelopmentStorage=true";
            process.StartInfo.Environment["AzureServiceBus__ConnectionString"] =
                "Endpoint=sb://boundary.invalid/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
            process.StartInfo.Environment["AzureFunctionsJobHost__Logging__Console__IsEnabled"] = "true";

            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Azure Functions Core Tools did not start.");
            }
            catch (Exception exception)
            {
                process.Dispose();
                await log.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Failed to start Azure Functions Core Tools. Ensure `func` is installed and available on PATH.", exception);
            }

            var logLines = Channel.CreateUnbounded<string>();
#pragma warning disable CA2025
            var logPumpTask = PumpLogsAsync(log, logLines.Reader, cancellationToken);
#pragma warning restore CA2025
            var host = new FunctionHost(
                process,
                log,
                logPath,
                logLines,
                logPumpTask,
                new Uri($"http://127.0.0.1:{port}/"));
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    logLines.Writer.TryWrite(args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    logLines.Writer.TryWrite(args.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try
            {
                await host.WaitForReadinessAsync(cancellationToken).ConfigureAwait(false);
                return host;
            }
            catch
            {
#pragma warning disable VSTHRD003
                await host.DisposeAsync().ConfigureAwait(false);
#pragma warning restore VSTHRD003
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);

            _process.CancelOutputRead();
            _process.CancelErrorRead();
            await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            _logLines.Writer.TryComplete();
#pragma warning disable VSTHRD003
            await _logPumpTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            await _log.DisposeAsync().ConfigureAwait(false);
            _process.Dispose();
        }

        private static async Task PumpLogsAsync(
            StreamWriter log,
            ChannelReader<string> logLines,
            CancellationToken cancellationToken)
        {
            await foreach (var line in logLines.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                await log.WriteLineAsync(SecretPattern.Replace(line, "$1[REDACTED]"), cancellationToken).ConfigureAwait(false);
        }

        private async Task WaitForReadinessAsync(CancellationToken cancellationToken)
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            var startupTimeoutTimestampDelta = (long)(StartupTimeout.TotalSeconds * Stopwatch.Frequency);
            var deadline = Stopwatch.GetTimestamp() + startupTimeoutTimestampDelta;
            while (Stopwatch.GetTimestamp() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_process.HasExited)
                    throw new InvalidOperationException(
                        $"Azure Functions host exited with code {_process.ExitCode}. Log: {_logPath}");
                try
                {
                    using var response = await client.GetAsync(
                        new Uri("healthCheck", UriKind.Relative),
                        cancellationToken).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.OK)
                        return;
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException($"Azure Functions health endpoint was not ready within {StartupTimeout}.");
        }

        private static string FindFunctionAppDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions/bin/Debug/net10.0");
                if (File.Exists(Path.Combine(candidate, "host.json")))
                    return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Set ARK_AZF_FUNCTION_APP_DIR to the built Azure Functions sample directory.");
        }

        private static int GetAvailablePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
