// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.ComponentModel;
using System.Diagnostics;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves the hosted gRPC reflection endpoint with the external grpc-curl tool.</summary>
[TestClass]
public sealed class GrpcReflectionTests
{
    /// <summary>Verifies grpc-curl discovers every generated versioned service.</summary>
    [TestMethod]
    public async Task DiscoversVersionedServicesThroughReflection()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcKestrelHostAsync(0).ConfigureAwait(false);

        var address = app.Urls.Single();
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("GRPC_CURL_PATH") ?? "grpc-curl",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--describe");
        startInfo.ArgumentList.Add(address);

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("grpc-curl did not start.");
        }
        catch (Win32Exception)
        {
            Assert.Inconclusive("Install grpc-curl to run the external reflection validation.");
            return;
        }

        using (process)
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            Assert.AreEqual(0, process.ExitCode, error);
            output.Should().Contain("service HostingV1");
            output.Should().Contain("service HostingV2");
            output.Should().Contain("service HostingV3");
        }
    }
}
