// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Xml.Linq;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Guards the Azure messaging package boundary split.</summary>
[TestClass]
public sealed class AzureMessagingPackageBoundaryTests
{
    [TestMethod]
    public void CoreMessagingProjectDoesNotReferenceAzureSdkPackages()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var project = XDocument.Load(Path.Join(
            root,
            "src",
            "mediator-framework",
            "Ark.Tools.MediatorFramework.Messaging",
            "Ark.Tools.MediatorFramework.Messaging.csproj"));

        var packageReferences = _packageReferences(project);

        packageReferences.Should().NotContain("Azure.Identity");
        packageReferences.Should().NotContain("Azure.Messaging.ServiceBus");
        packageReferences.Should().NotContain("Azure.Storage.Blobs");
        packageReferences.Should().NotContain("Azure.Storage.Queues");
    }

    [TestMethod]
    public void AzureMessagingProjectIsIncludedInSolutionAndFunctionsHost()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var azureProject = Path.Join(
            root,
            "src",
            "mediator-framework",
            "Ark.Tools.MediatorFramework.Messaging.Azure",
            "Ark.Tools.MediatorFramework.Messaging.Azure.csproj");

        File.Exists(azureProject).Should().BeTrue();
        if (!File.Exists(azureProject))
            return;

        var project = XDocument.Load(azureProject);
        var packageReferences = _packageReferences(project);
        packageReferences.Should().Contain("Azure.Identity");
        packageReferences.Should().Contain("Azure.Messaging.ServiceBus");
        packageReferences.Should().Contain("Azure.Storage.Blobs");
        packageReferences.Should().Contain("Azure.Storage.Queues");

        var solution = File.ReadAllText(Path.Join(root, "Ark.Tools.slnx"));
        solution.Should().Contain(
            "src/mediator-framework/Ark.Tools.MediatorFramework.Messaging.Azure/Ark.Tools.MediatorFramework.Messaging.Azure.csproj");

        var azureFunctions = XDocument.Load(Path.Join(
            root,
            "src",
            "mediator-framework",
            "Ark.Tools.MediatorFramework.AzureFunctions",
            "Ark.Tools.MediatorFramework.AzureFunctions.csproj"));
        _projectReferences(azureFunctions).Should().Contain(
            @"..\Ark.Tools.MediatorFramework.Messaging.Azure\Ark.Tools.MediatorFramework.Messaging.Azure.csproj"
                .Replace('\\', Path.DirectorySeparatorChar));
    }

    private static IReadOnlyCollection<string> _packageReferences(XDocument project)
    {
        return project.Root!
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyCollection<string> _projectReferences(XDocument project)
    {
        return project.Root!
            .Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();
    }
}
