// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using AwesomeAssertions;

using System.Diagnostics;
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

    /// <summary>Verifies an assembly without generated services is a successful no-op.</summary>
    [TestMethod]
    public async Task ExportRunnerNoOpsWithoutGeneratedServices()
    {
        var destination = _createTemporaryDirectory();
        try
        {
            var result = await _runExporterAsync(
                _findBuiltAssembly("Ark.Tools.MediatorFramework.Hosting.GrpcClient"),
                destination).ConfigureAwait(false);

            result.ExitCode.Should().Be(0, result.Error);
            Directory.Exists(destination).Should().BeFalse();
        }
        finally
        {
            _deleteTemporaryDirectory(destination);
        }
    }

    /// <summary>Verifies repeated exports preserve unchanged generated files.</summary>
    [TestMethod]
    public async Task ExportRunnerPreservesUnchangedFiles()
    {
        var destination = _createTemporaryDirectory();
        try
        {
            var result = await _runExporterAsync(
                _findBuiltAssembly("Ark.Tools.MediatorFramework.Hosting.Contracts"),
                destination).ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Error);

            var proto = Path.Combine(destination, "Hosting.proto");
            File.Exists(proto).Should().BeTrue();
            var expectedTimestamp = DateTime.UtcNow.AddMinutes(-1);
            File.SetLastWriteTimeUtc(proto, expectedTimestamp);

            result = await _runExporterAsync(
                _findBuiltAssembly("Ark.Tools.MediatorFramework.Hosting.Contracts"),
                destination).ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Error);
            File.GetLastWriteTimeUtc(proto).Should().Be(expectedTimestamp);
        }
        finally
        {
            _deleteTemporaryDirectory(destination);
        }
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

    private static string _findBuiltAssembly(string projectName)
    {
        var repositoryRoot = _findRepositoryRoot();
        var targetFramework = _targetFramework();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(
            repositoryRoot.FullName,
            "tests",
            projectName,
            "bin",
            configuration,
            targetFramework,
            projectName + ".dll");
        File.Exists(assembly).Should().BeTrue();
        return assembly;
    }

    private static string _findExporter()
    {
        var repositoryRoot = _findRepositoryRoot();
        var targetFramework = _targetFramework();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var exporter = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "mediator-framework",
            "Ark.Tools.MediatorFramework.Grpc.Export",
            "bin",
            configuration,
            targetFramework,
            "Ark.Tools.MediatorFramework.Grpc.Export.dll");
        File.Exists(exporter).Should().BeTrue();
        return exporter;
    }

    private static async Task<(int ExitCode, string Error)> _runExporterAsync(
        string assembly,
        string destination)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add(_findExporter());
        process.StartInfo.ArgumentList.Add(assembly);
        process.StartInfo.ArgumentList.Add(destination);
        process.Start();
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode, error);
    }

    private static DirectoryInfo _findRepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Ark.Tools.slnx")))
        {
            root = root.Parent;
        }

        root.Should().NotBeNull();
        return root!;
    }

    private static string _targetFramework()
    {
        var frameworkName = AppContext.TargetFrameworkName
            ?? throw new InvalidOperationException("The test target framework was not available.");
        var version = new FrameworkName(frameworkName).Version;
        return $"net{version.Major}.{version.Minor}";
    }

    private static string _createTemporaryDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "ark-gen07-" + Guid.NewGuid().ToString("N"));
    }

    private static void _deleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
