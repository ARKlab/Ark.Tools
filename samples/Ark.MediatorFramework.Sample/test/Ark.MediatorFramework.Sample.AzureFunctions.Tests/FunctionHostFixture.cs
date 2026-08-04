// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Ark.MediatorFramework.Sample.AzureFunctions.Tests;

/// <summary>
/// Launches the built Azure Functions sample host with Azure Functions Core Tools (`func`).
/// This demonstrates how to boundary-test a Mediator-Framework application hosted as an
/// Azure Function: build the host project, start `func start` against its output folder,
/// and wait for the generated anonymous <c>/healthCheck</c> endpoint before running tests.
/// </summary>
internal sealed class FunctionHostFixture : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private readonly Process _process;

    private FunctionHostFixture(Process process, Uri baseAddress)
    {
        _process = process;
        BaseAddress = baseAddress;
    }

    /// <summary>Gets the base address of the running Functions host.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Starts the Functions host and waits for readiness.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The running host.</returns>
    public static async Task<FunctionHostFixture> StartAsync(CancellationToken cancellationToken)
    {
        var appDirectory = FindFunctionAppDirectory();
        var port = GetAvailablePort();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "func",
                Arguments = $"start --port {port} --dotnet-isolated",
                WorkingDirectory = appDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        // Local settings an Azure Functions test host needs:
        // - AzureWebJobsStorage: Azurite (started via docker) stands in for the real storage account.
        // - Service Bus: any syntactically valid connection string works for an outbound-only bus that never sends.
        // - ASPNETCORE_ENVIRONMENT=IntegrationTests: the sample host swaps Entra ID for a symmetric-key JWT scheme.
        process.StartInfo.Environment["AzureWebJobsScriptRoot"] = appDirectory;
        process.StartInfo.Environment["AzureWebJobsStorage"] = "UseDevelopmentStorage=true";
        process.StartInfo.Environment["AzureServiceBus__ConnectionString"] =
            "Endpoint=sb://sample.invalid/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests";

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Azure Functions Core Tools did not start.");
        }
        catch (Exception exception)
        {
            process.Dispose();
            throw new InvalidOperationException(
                "Failed to start Azure Functions Core Tools. Ensure `func` is installed and available on PATH.", exception);
        }

        var host = new FunctionHostFixture(process, new Uri($"http://127.0.0.1:{port}/"));
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
            _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        _process.Dispose();
    }

    private async Task WaitForReadinessAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };
        var deadline = Stopwatch.GetTimestamp() + (long)(StartupTimeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process.HasExited)
                throw new InvalidOperationException($"Azure Functions host exited with code {_process.ExitCode}.");
            try
            {
                using var response = await client.GetAsync(
                    new Uri("healthCheck", UriKind.Relative), cancellationToken).ConfigureAwait(false);
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
        // The Functions host project is referenced by this test project, so it is built alongside it;
        // walk up from the test output to the sample host's own output folder that contains host.json.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src/Ark.MediatorFramework.Sample.AzureFunctions/bin/Debug/net10.0");
            if (File.Exists(Path.Combine(candidate, "host.json")))
                return candidate;
            directory = directory.Parent!;
        }

        throw new DirectoryNotFoundException("Could not locate the built Azure Functions sample host.");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
