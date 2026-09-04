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

            var proto = Path.Join(destination, "Hosting.proto");
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

    /// <summary>Verifies the exporter rejects generated paths that escape the destination.</summary>
    [TestMethod]
    public async Task ExportRunnerRejectsEscapingGeneratedPaths()
    {
        var fixture = _createTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(fixture);
            var project = Path.Join(fixture, "Malicious.csproj");
            await File.WriteAllTextAsync(
                project,
                """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
""").ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Join(fixture, "Program.cs"),
                """
namespace Ark.Tools.MediatorFramework.Generated;

public static class ArkGeneratedProtos
{
    public static (string, string)[] GetFiles() => [("../escape.proto", "invalid")];
}

public static class Program
{
    public static void Main()
    {
    }
}
""").ConfigureAwait(false);
            var build = await _runDotnetAsync("build", fixture, project).ConfigureAwait(false);
            build.ExitCode.Should().Be(0, build.Output);

            var destination = Path.Join(fixture, "proto");
            var result = await _runExporterAsync(
                Path.Join(fixture, "bin", "Debug", "net10.0", "Malicious.dll"),
                destination).ConfigureAwait(false);
            result.ExitCode.Should().NotBe(0);
            Directory.Exists(destination).Should().BeFalse();
            File.Exists(Path.Join(fixture, "escape.proto")).Should().BeFalse();
        }
        finally
        {
            _deleteTemporaryDirectory(fixture);
        }
    }

    /// <summary>Verifies packed build assets export generated and additional protos without starting the consumer.</summary>
    [TestMethod]
    public async Task PackedConsumerExportsWithoutStartupAndSupportsBuildOptions()
    {
        var fixture = _createTemporaryDirectory();
        try
        {
            var feed = await _packGrpcClosureAsync(fixture).ConfigureAwait(false);
            var consumer = Path.Join(fixture, "consumer");
            Directory.CreateDirectory(consumer);
            await File.WriteAllTextAsync(Path.Join(consumer, "hand.proto"), "syntax = \"proto3\";\n").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(consumer, "Consumer.csproj"), _packedProject("""
<PropertyGroup>
  <ArkExportProtoDir>$(MSBuildProjectDirectory)/custom-proto</ArkExportProtoDir>
</PropertyGroup>
<ItemGroup>
  <ArkAdditionalProto Include="hand.proto" />
</ItemGroup>
""")).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(consumer, "Program.cs"), _generatedConsumerSource()).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(consumer, "NuGet.Config"), _nugetConfig(feed)).ConfigureAwait(false);

            var result = await _runDotnetAsync(
                "build",
                consumer,
                consumer,
                "--configfile", Path.Join(consumer, "NuGet.Config")).ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Output);

            var output = Path.Join(consumer, "custom-proto");
            File.Exists(Path.Join(output, "Consumer.proto")).Should().BeTrue();
            File.Exists(Path.Join(output, "ark", "mediator.proto")).Should().BeTrue();
            File.Exists(Path.Join(output, "ark", "nodatime.proto")).Should().BeTrue();
            File.Exists(Path.Join(output, "hand.proto")).Should().BeTrue();
            File.Exists(Path.Join(consumer, "started.txt")).Should().BeFalse();

            var generatedProto = Path.Join(output, "Consumer.proto");
            var timestamp = DateTime.UtcNow.AddMinutes(-1);
            File.SetLastWriteTimeUtc(generatedProto, timestamp);
            result = await _runDotnetAsync("build", consumer, consumer, "--no-restore").ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Output);
            File.GetLastWriteTimeUtc(generatedProto).Should().Be(timestamp);

            Directory.Delete(output, recursive: true);
            result = await _runDotnetAsync(
                "build",
                consumer,
                consumer,
                "--no-restore", "-p:ArkExportProto=false").ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Output);
            Directory.Exists(output).Should().BeFalse();
        }
        finally
        {
            _deleteTemporaryDirectory(fixture);
        }
    }

    /// <summary>Verifies a packed gRPC reference is a no-op when the consumer has no generated service.</summary>
    [TestMethod]
    public async Task PackedConsumerWithoutGeneratedServicesDoesNotStartOrExport()
    {
        var fixture = _createTemporaryDirectory();
        try
        {
            var feed = await _packGrpcClosureAsync(fixture).ConfigureAwait(false);
            var consumer = Path.Join(fixture, "consumer");
            Directory.CreateDirectory(consumer);
            await File.WriteAllTextAsync(Path.Join(consumer, "Consumer.csproj"), _packedProject(string.Empty)).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Join(consumer, "Program.cs"),
                "using System.IO;\nFile.WriteAllText(\"started.txt\", \"started\");\n").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(consumer, "NuGet.Config"), _nugetConfig(feed)).ConfigureAwait(false);

            var result = await _runDotnetAsync(
                "build",
                consumer,
                consumer,
                "--configfile", Path.Join(consumer, "NuGet.Config")).ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Output);
            Directory.Exists(Path.Join(consumer, "proto")).Should().BeFalse();
            File.Exists(Path.Join(consumer, "started.txt")).Should().BeFalse();
        }
        finally
        {
            _deleteTemporaryDirectory(fixture);
        }
    }

    private static string _findExportedProto()
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repositoryRoot is not null
            && !File.Exists(Path.Join(repositoryRoot.FullName, "Ark.Tools.slnx")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        repositoryRoot.Should().NotBeNull();
        var contractsObj = Path.Join(
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
        var assembly = Path.Join(
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
        var exporter = Path.Join(
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

    private static async Task<string> _packGrpcClosureAsync(string fixture)
    {
        var root = _findRepositoryRoot().FullName;
        var feed = Path.Join(fixture, "feed");
        Directory.CreateDirectory(feed);
        var projects = new[]
        {
            "src/common/Ark.Tools.Authorization/Ark.Tools.Authorization.csproj",
            "src/common/Ark.Tools.Core/Ark.Tools.Core.csproj",
            "src/common/Ark.Tools.Nodatime/Ark.Tools.Nodatime.csproj",
            "src/common/Ark.Tools.Nodatime.Protobuf/Ark.Tools.Nodatime.Protobuf.csproj",
            "src/common/Ark.Tools.Nodatime.SystemTextJson/Ark.Tools.Nodatime.SystemTextJson.csproj",
            "src/common/Ark.Tools.NLog/Ark.Tools.NLog.csproj",
            "src/common/Ark.Tools.Outbox/Ark.Tools.Outbox.csproj",
            "src/common/Ark.Tools.Solid/Ark.Tools.Solid.csproj",
            "src/common/Ark.Tools.SystemTextJson/Ark.Tools.SystemTextJson.csproj",
            "src/mediator-framework/Ark.Tools.MediatorFramework/Ark.Tools.MediatorFramework.csproj",
            "src/mediator-framework/Ark.Tools.MediatorFramework.Grpc/Ark.Tools.MediatorFramework.Grpc.csproj",
        };
        foreach (var project in projects)
        {
            var result = await _runDotnetAsync(
                "pack",
                root,
                Path.Join(root, project),
                "--no-build", "-c", "Debug", "-o", feed, "-p:TargetFrameworks=net10.0",
                "-p:PackageVersion=999.9.20", "-p:TreatWarningsAsErrors=false", "-p:NoWarn=NU5128").ConfigureAwait(false);
            result.ExitCode.Should().Be(0, result.Output);
        }

        return feed;
    }

    private static string _packedProject(string propertiesAndItems)
    {
        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <ArkApiSurfaceEnabled>false</ArkApiSurfaceEnabled>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
  </PropertyGroup>
  {{propertiesAndItems}}
  <ItemGroup>
    <PackageReference Include="Ark.Tools.MediatorFramework.Grpc" Version="999.9.20" />
    <PackageReference Include="SimpleInjector" Version="5.6.0" />
  </ItemGroup>
</Project>
""";
    }

    private static string _generatedConsumerSource()
    {
        return """
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.Solid;

public sealed class Marker
{
}

[ArkGenerateGrpcForAssembly(typeof(Marker))]
public partial class Context
{
}

[GrpcMethod, GrpcService("Consumer")]
[ProtoBuf.ProtoContract]
public sealed record Ping : IRequest<Ping, Pong>
{
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

[ProtoBuf.ProtoContract]
public sealed record Pong
{
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

public static class Startup
{
    public static void Main()
    {
        File.WriteAllText("started.txt", "started");
    }
}
""";
    }

    private static string _nugetConfig(string feed)
    {
        return $"""
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="{feed}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
""";
    }

    private static async Task<(int ExitCode, string Output)> _runDotnetAsync(
        string command,
        string workingDirectory,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add(command);
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        output += await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode, output);
    }

    private static DirectoryInfo _findRepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Join(root.FullName, "Ark.Tools.slnx")))
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
        return Path.Join(Path.GetTempPath(), "ark-gen07-" + Guid.NewGuid().ToString("N"));
    }

    private static void _deleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
