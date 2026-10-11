// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Reflection;
using System.Text.Json.Nodes;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>
/// Each Functions app indexes exactly the functions of its own Triggers library. The Worker SDK writes the index to
/// <c>functions.metadata</c> at build.
/// </summary>
[TestClass]
public sealed class FunctionsIndexingTests
{
    [TestMethod]
    public void ApiIndexesOnlyHttpTriggers()
    {
        var functions = _indexedFunctions("Api");

        functions.Should().NotBeEmpty();
        functions.Select(static f => f.Trigger).Should().AllBe("httpTrigger");
    }

    [TestMethod]
    [DataRow("Processor", "ark-mediator-sample")]
    [DataRow("Notifications", "sample-messaging-notification")]
    [DataRow("Audit", "sample-messaging-audit")]
    public void ReceiverIndexesOnlyItsServiceBusTrigger(string app, string function)
    {
        _indexedFunctions(app).Should().Equal(("serviceBusTrigger", function));
    }

    private static List<(string Trigger, string Name)> _indexedFunctions(string app)
    {
        var metadata = typeof(FunctionsIndexingTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
        var directory = metadata.Single(static a => a.Key == "FunctionsAppsDirectory").Value!;
        var configuration = metadata.Single(static a => a.Key == "FunctionsAppsConfiguration").Value!;
        var project = "Ark.MediatorFramework.Sample.Core.Functions." + app;
        var path = Path.Combine(directory, project, "bin", configuration, "net10.0", "linux-x64", "functions.metadata");

        return JsonNode.Parse(File.ReadAllText(path))!.AsArray()
            .Select(static f => (f!["bindings"]![0]!["type"]!.GetValue<string>(), f["name"]!.GetValue<string>()))
            .ToList();
    }
}
