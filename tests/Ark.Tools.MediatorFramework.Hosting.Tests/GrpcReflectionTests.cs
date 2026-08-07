// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.ComponentModel;
using System.Diagnostics;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves the hosted gRPC reflection endpoint via the grpcurl Docker image.</summary>
[TestClass]
public sealed class GrpcReflectionTests
{
    private const int GrpcPort = 50051;

    /// <summary>Verifies grpcurl discovers every generated versioned service via the reflection endpoint.</summary>
    [TestMethod]
    public async Task DiscoversVersionedServicesThroughReflection()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcKestrelHostAsync(GrpcPort).ConfigureAwait(false);

        // Use the Docker image so there is no local grpcurl install requirement.
        // --network="host" lets the container reach the host loopback on the same port.
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--rm");
        startInfo.ArgumentList.Add("--network=host");
        startInfo.ArgumentList.Add("fullstorydev/grpcurl:latest");
        startInfo.ArgumentList.Add("-plaintext");
        startInfo.ArgumentList.Add($"localhost:{GrpcPort}");
        startInfo.ArgumentList.Add("list");

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("docker did not start.");
        }
        catch (Win32Exception)
        {
            Assert.Inconclusive("Install Docker to run the external reflection validation.");
            return;
        }

        using (process)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            Assert.AreEqual(0, process.ExitCode, error);
            output.Should().Contain("HostingV1");
            output.Should().Contain("HostingV2");
            output.Should().Contain("HostingV3");
        }
    }
}
