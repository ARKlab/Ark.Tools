// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using AwesomeAssertions;

using System.Runtime.Versioning;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves exported gRPC proto text and generated client shape.</summary>
[TestClass]
public sealed class GrpcProtoExportTests
{
    /// <summary>Verifies the clean-build proto contains the generated services and methods.</summary>
    [TestMethod]
    public void ExportsDeterministicProtoServices()
    {
        var protoPath = _findExportedProto();
        var proto = File.ReadAllText(protoPath);

        proto.Should().Contain("service HostingV1");
        proto.Should().Contain("service HostingV2");
        proto.Should().Contain("service HostingV3");
        proto.Should().Contain("rpc HostingRequest(HostingRequestMessage) returns (HostingResponse);");
        proto.Should().Contain("rpc UploadHostingAttachment(stream ark.mediator.UploadDocumentChunk)");
        proto.Should().Contain("rpc DownloadHostingAttachment(HostingAttachmentDownloadQuery) returns (stream DownloadDocumentChunk);");
        proto.Should().NotContain("import \"ark/nodatime.proto\";");
    }

    /// <summary>Verifies ETag-marked contract properties stay in the exported proto messages.</summary>
    [TestMethod]
    public void ExportsETagFieldsInProtoMessages()
    {
        var protoPath = _findExportedProto();
        var proto = File.ReadAllText(protoPath);

        var messageIndex = proto.IndexOf("message HostingETagMismatchRequest {", StringComparison.Ordinal);
        messageIndex.Should().BeGreaterThan(-1);
        var messageEnd = proto.IndexOf('}', messageIndex);
        proto[messageIndex..messageEnd].Should().Contain("string e_tag = 1;");
    }

    /// <summary>Verifies generated clients expose the exported versioned service descriptors.</summary>
    [TestMethod]
    public void GeneratesVersionedClientShape()
    {
        var services = HostingReflection.Descriptor.Services
            .Select(service => service.Name)
            .ToArray();

        services.Should().Equal("HostingV1", "HostingV2", "HostingV3");
        typeof(HostingV1.HostingV1Client).Should().NotBeNull();
        typeof(HostingV2.HostingV2Client).Should().NotBeNull();
        typeof(HostingV3.HostingV3Client).Should().NotBeNull();
        HostingReflection.Descriptor.Services
            .Single(service => service.Name == "HostingV1")
            .Methods.Should().Contain(method => method.Name == "HostingRequest");
    }

    private static string _findExportedProto()
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repositoryRoot is not null
            && !File.Exists(Path.Combine(repositoryRoot.FullName, "Ark.Tools.slnx")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        repositoryRoot.Should().NotBeNull();
        var contractsObj = Path.Combine(
            repositoryRoot!.FullName,
            "tests",
            "Ark.Tools.MediatorFramework.Hosting.Contracts",
            "obj");
        var frameworkName = AppContext.TargetFrameworkName
            ?? throw new InvalidOperationException("The test target framework was not available.");
        var version = new FrameworkName(frameworkName).Version;
        var targetFramework = $"net{version.Major}.{version.Minor}";
        var proto = Directory.GetFiles(contractsObj, "Hosting.proto", SearchOption.AllDirectories)
            .SingleOrDefault(path =>
                path.Contains(
                    $"{Path.DirectorySeparatorChar}{targetFramework}{Path.DirectorySeparatorChar}ark-proto{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase));
        proto.Should().NotBeNull();
        return proto!;
    }
}
