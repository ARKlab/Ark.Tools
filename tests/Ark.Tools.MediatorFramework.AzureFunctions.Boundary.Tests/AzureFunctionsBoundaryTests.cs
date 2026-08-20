// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;

using AwesomeAssertions;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;

using Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests;

[TestClass]
public sealed class AzureFunctionsBoundaryTests
{
    private static FunctionHost? _host;
    private static HttpClient? _client;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _host = await FunctionHost.StartAsync(context.CancellationToken).ConfigureAwait(false);
        _client = new HttpClient { BaseAddress = _host.BaseAddress };
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        _client?.Dispose();
        if (_host is not null)
            await _host.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task HealthEndpointIsDiscoveredByCoreTools()
    {
        using var response = await _client!.GetAsync(
            new Uri("healthCheck", UriKind.Relative),
            TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task AnonymousEndpointIsReachableWithoutCredentials()
    {
        using var response = await _client!.GetAsync(
            new Uri("api/v1/ping", UriKind.Relative),
            TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
        body.Should().Contain("pong");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task UnauthenticatedRequestIsChallenged()
    {
        using var response = await _client!.GetAsync(
            new Uri($"api/v1/echo/{Guid.NewGuid()}", UriKind.Relative),
            TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task RouteAndQueryValuesAreBoundIntoTheContract()
    {
        var id = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"api/v1/echo/{id}?Message=hello&Count=3", UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTokenBuilder.Build("boundary-user"));

        using var response = await _client!.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
        body.Should().Contain(id.ToString()).And.Contain("hello").And.Contain("3");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task JsonBodyIsBoundIntoTheRecordContract()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("api/v1/echo", UriKind.Relative))
        {
            Content = new StringContent("""{"Message":"from-body"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTokenBuilder.Build("boundary-user"));

        using var response = await _client!.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
        body.Should().Contain("from-body");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task ValidationFailureProducesProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"api/v1/echo/{Guid.NewGuid()}?Count=0", UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTokenBuilder.Build("boundary-user"));

        using var response = await _client!.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
        body.Should().Contain("Count");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task BindingFailureProducesProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"api/v1/echo/{Guid.NewGuid()}?Count=not-a-number", UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTokenBuilder.Build("boundary-user"));

        using var response = await _client!.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
        body.Should().Contain("BINDING_FAILURE");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task InvalidJsonBodyProducesProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("api/v1/echo", UriKind.Relative))
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTokenBuilder.Build("boundary-user"));

        using var response = await _client!.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public void TestHostEndpointsMatchTheParityMatrix()
    {
        var hostMarker = typeof(EchoQuery).Assembly
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

        actual.Should().Equal(_expectedEndpoints.OrderBy(row => row.TypeName, StringComparer.Ordinal));
    }

    private static readonly EndpointRow[] _expectedEndpoints =
    [
        new("EchoQuery", "GET", "/api/v{version}/echo/{id}"),
        new("EchoRequest", "POST", "/api/v{version}/echo"),
        new("PingQuery", "GET", "/api/v{version}/ping"),
    ];

    private sealed record EndpointRow(string TypeName, string Verb, string Route);

    private sealed class FunctionHost : IAsyncDisposable
    {
        private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(60);
        private static readonly Regex _secretPattern = new(
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
                ?? _findFunctionAppDirectory();
            var port = _getAvailablePort();
            var logPath = Path.Combine(Path.GetTempPath(), $"ark-azf-{Guid.NewGuid():N}.log");
            var log = new StreamWriter(logPath) { AutoFlush = true };
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "func",
                    Arguments = OperatingSystem.IsWindows()
                        ? $"/d /c func.cmd start --port {port} --dotnet-isolated --verbose"
                        : $"start --port {port} --dotnet-isolated --verbose",
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
            process.StartInfo.Environment["AzureFunctionsJobHost__Logging__Console__IsEnabled"] = "true";
            process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests";

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
            var logPumpTask = _pumpLogsAsync(log, logLines.Reader, cancellationToken);
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
                await host._waitForReadinessAsync(cancellationToken).ConfigureAwait(false);
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

        private static async Task _pumpLogsAsync(
            StreamWriter log,
            ChannelReader<string> logLines,
            CancellationToken cancellationToken)
        {
            await foreach (var line in logLines.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                await log.WriteLineAsync(_secretPattern.Replace(line, "$1[REDACTED]"), cancellationToken).ConfigureAwait(false);
        }

        private async Task _waitForReadinessAsync(CancellationToken cancellationToken)
        {
            using var client = new HttpClient { BaseAddress = BaseAddress };
            var startupTimeoutTimestampDelta = (long)(_startupTimeout.TotalSeconds * Stopwatch.Frequency);
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

            throw new TimeoutException($"Azure Functions health endpoint was not ready within {_startupTimeout}.");
        }

        private static string _findFunctionAppDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.TestHost/bin/Debug/net10.0");
                if (File.Exists(Path.Combine(candidate, "host.json")))
                    return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Set ARK_AZF_FUNCTION_APP_DIR to the built Azure Functions sample directory.");
        }

        private static int _getAvailablePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
