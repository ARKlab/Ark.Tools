// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using OpenTelemetry.Resources;

using System.Reflection;

namespace Ark.Tools.OTel.Tests;

[TestClass]
public sealed class ArkTelemetryResourceTests
{
    [TestMethod]
    public void AddArkTelemetryResource_AddsEntryAssemblyAsServiceName()
    {
        var serviceName = Assembly.GetEntryAssembly()?.GetName().Name;
        var resource = ResourceBuilder.CreateEmpty()
            .AddArkTelemetryResource()
            .Build();

        resource.Attributes.Should().Contain(static x => x.Key == "service.name");
        resource.Attributes.Single(static x => x.Key == "service.name").Value.Should().Be(serviceName);
    }
}
