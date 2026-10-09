// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.TestHost;

using System.Reflection;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Pins the generated OpenAPI document of the Web host.</summary>
[TestClass]
public sealed class OpenApiDocumentTests
{
    /// <summary>Compares the v1 document with the committed snapshot.</summary>
    [TestMethod]
    public async Task V1DocumentMatchesSnapshot()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;

        using var response = await host.App.GetTestClient().GetAsync(
            new Uri("/openapi/v1.json", UriKind.Relative),
            ctk).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var received = _normalize(await response.Content.ReadAsStringAsync(ctk).ConfigureAwait(false));

        var directory = typeof(OpenApiDocumentTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "SnapshotDirectory").Value!;
        var snapshotPath = Path.Combine(directory, "openapi.v1.json");
        var receivedPath = Path.Combine(directory, "openapi.v1.received.json");
        var snapshot = File.Exists(snapshotPath)
            ? _normalize(await File.ReadAllTextAsync(snapshotPath, ctk).ConfigureAwait(false))
            : null;
        if (string.Equals(snapshot, received, StringComparison.Ordinal))
        {
            File.Delete(receivedPath);
            return;
        }

        await File.WriteAllTextAsync(receivedPath, received, ctk).ConfigureAwait(false);
        Assert.Fail($"The OpenAPI v1 document differs from the snapshot. Review {receivedPath} and, if the change is intended, copy it over {snapshotPath}.");
    }

    private static string _normalize(string text)
    {
        return text.ReplaceLineEndings("\n");
    }
}
