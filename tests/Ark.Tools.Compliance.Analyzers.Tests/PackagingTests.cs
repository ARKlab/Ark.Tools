// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.IO.Compression;

namespace Ark.Tools.Compliance.Analyzers.Tests;

/// <summary>Verifies the standalone compliance analyzer package ships analyzer assets at the TFM-free path.</summary>
[TestClass]
public sealed class PackagingTests
{
    /// <summary>The analyzer DLL is packed under analyzers/dotnet/cs instead of a TFM-specific subfolder.</summary>
    [TestMethod]
    public async Task PackageContainsAnalyzerAtTfMFreePath()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var feed = Path.Join(root, "artifacts", "compliance-analyzers-pack-test");
        Directory.CreateDirectory(feed);
        var project = Path.Join(root, "src", "compliance", "Ark.Tools.Compliance.Analyzers", "Ark.Tools.Compliance.Analyzers.csproj");
        await _runAsync("dotnet", $"pack \"{project}\" -c Debug -o \"{feed}\" -p:PackageVersion=999.9.9").ConfigureAwait(false);

        var archive = await ZipFile.OpenReadAsync(Path.Join(feed, "Ark.Tools.Compliance.Analyzers.999.9.9.nupkg")).ConfigureAwait(false);
        await using (archive.ConfigureAwait(false))
        {
            Assert.IsNotNull(archive.GetEntry("analyzers/dotnet/cs/Ark.Tools.Compliance.Analyzers.dll"));
            Assert.IsNull(archive.GetEntry("analyzers/dotnet/cs/netstandard2.0/Ark.Tools.Compliance.Analyzers.dll"));
            Assert.IsNull(archive.GetEntry("lib/netstandard2.0/Ark.Tools.Compliance.Analyzers.dll"));
        }
    }

    private static async Task _runAsync(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false) + await errorTask.ConfigureAwait(false);
        Assert.AreEqual(0, process.ExitCode, output);
    }
}
