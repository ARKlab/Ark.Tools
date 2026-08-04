// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.AzureFunctions;

using AwesomeAssertions;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests;

[TestClass]
public sealed class AzureFunctionsBoundaryTests
{
    private static FunctionHost? _host;

    [ClassInitialize]
    public static async Task StartHost(TestContext context)
    {
        _host = await FunctionHost.StartAsync(context.CancellationToken);
    }

    [ClassCleanup]
    public static async Task StopHost()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task HealthEndpointIsDiscoveredByCoreTools()
    {
        using var client = new HttpClient
        {
            BaseAddress = _host!.BaseAddress,
        };

        using var response = await client.GetAsync("healthCheck");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public void SelectedApplicationEndpointsMatchTheParityMatrix()
    {
        var hostMarker = typeof(Program).Assembly
            .GetCustomAttributes<HttpHostAttribute>()
            .Single();
        var excluded = hostMarker.ExcludedContracts.ToHashSet();
        var actual = hostMarker.ContractAssemblyMarker.Assembly
            .GetTypes()
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<HttpEndpointAttribute>()))
            .Where(item => item.Attribute is not null && !excluded.Contains(item.Type))
            .Select(item => new EndpointRow(
                item.Type.Name,
                item.Attribute!.Verb,
                item.Attribute.Template))
            .OrderBy(row => row.TypeName)
            .ToArray();

        actual.Should().Equal(ExpectedEndpoints.OrderBy(row => row.TypeName));
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
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly Process _process;
        private readonly StreamWriter _log;

        private FunctionHost(Process process, StreamWriter log, Uri baseAddress)
        {
            _process = process;
            _log = log;
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
                    Arguments = $"start --port {port} --verbose",
                    WorkingDirectory = appDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
            process.StartInfo.Environment["AzureWebJobsScriptRoot"] = appDirectory;
            process.StartInfo.Environment["AzureServiceBus__ConnectionString"] =
                "boundary.invalid";
            process.StartInfo.Environment["AzureFunctionsJobHost__Logging__Console__IsEnabled"] = "true";

            if (!process.Start())
                throw new InvalidOperationException("Azure Functions Core Tools did not start.");

            _ = CaptureAsync(process.StandardOutput, log);
            _ = CaptureAsync(process.StandardError, log);
            var host = new FunctionHost(process, log, new Uri($"http://127.0.0.1:{port}/"));
            try
            {
                await host.WaitForReadinessAsync(cancellationToken);
                return host;
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
            await _log.DisposeAsync();
        }

        private async Task WaitForReadinessAsync(CancellationToken cancellationToken)
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            var deadline = Stopwatch.GetTimestamp() + StartupTimeout.Ticks * (Stopwatch.Frequency / TimeSpan.TicksPerSecond);
            while (Stopwatch.GetTimestamp() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_process.HasExited)
                    throw new InvalidOperationException($"Azure Functions host exited with code {_process.ExitCode}.");
                try
                {
                    using var response = await client.GetAsync("healthCheck", cancellationToken);
                    if (response.StatusCode == HttpStatusCode.OK)
                        return;
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }

            throw new TimeoutException($"Azure Functions health endpoint was not ready within {StartupTimeout}.");
        }

        private static async Task CaptureAsync(StreamReader reader, StreamWriter log)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                await log.WriteLineAsync(SecretPattern.Replace(line, "$1[REDACTED]")).ConfigureAwait(false);
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
