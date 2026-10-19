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

    [TestMethod]
    public void AddArkTelemetryResource_AddsEntryAssemblyInformationalVersionAsServiceVersion()
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var resource = ResourceBuilder.CreateEmpty()
            .AddArkTelemetryResource()
            .Build();

        version.Should().NotBeNullOrEmpty();
        resource.Attributes.Single(static x => x.Key == "service.version").Value.Should().Be(version);
    }

    [TestMethod]
    [DoNotParallelize] // Sets process environment variables.
    public void AddArkTelemetryResource_LetsEnvironmentVariablesOverrideNameAndVersion()
    {
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
        var resourceAttributes = Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES");
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", "custom-service");
        Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", "service.version=9.9.9");
        try
        {
            var resource = ResourceBuilder.CreateEmpty()
                .AddArkTelemetryResource()
                .Build();

            resource.Attributes.Single(static x => x.Key == "service.name").Value.Should().Be("custom-service");
            resource.Attributes.Single(static x => x.Key == "service.version").Value.Should().Be("9.9.9");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", serviceName);
            Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", resourceAttributes);
        }
    }
}
