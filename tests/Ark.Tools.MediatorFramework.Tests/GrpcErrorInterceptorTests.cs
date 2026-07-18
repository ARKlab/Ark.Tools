// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Grpc;

using AwesomeAssertions;

using Google.Protobuf.WellKnownTypes;
using DebugInfo = Google.Rpc.DebugInfo;
using RpcStatus = Google.Rpc.Status;

using Grpc.Core;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies environment-aware gRPC exception detail serialization.</summary>
[TestClass]
public sealed class GrpcErrorInterceptorTests
{
    [TestMethod]
    public async Task HidesUnexpectedExceptionDetailsOutsideDevelopment()
    {
        var interceptor = new ArkGrpcErrorInterceptor(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new ArkGrpcErrorOptions()));

        Func<Task> action = interceptor.AwaitUnexpectedException;
        var exception = await action
            .Should()
            .ThrowAsync<RpcException>();

        exception.Which.StatusCode.Should().Be(StatusCode.Internal);
        exception.Which.Status.Detail.Should().Be("An unexpected error occurred.");
    }

    [TestMethod]
    public async Task IncludesUnexpectedExceptionDetailsInDevelopment()
    {
        var interceptor = new ArkGrpcErrorInterceptor(
            new TestHostEnvironment(Environments.Development),
            Options.Create(new ArkGrpcErrorOptions()));

        Func<Task> action = interceptor.AwaitUnexpectedException;
        var exception = await action
            .Should()
            .ThrowAsync<RpcException>();

        exception.Which.Status.Detail.Should().Be("grpc exception detail");
        var status = RpcStatus.Parser.ParseFrom(
            exception.Which.Trailers.GetValueBytes("grpc-status-details-bin"));
        status.Details.Should().Contain(detail => detail.Is(DebugInfo.Descriptor));
        status.Details
            .Single(detail => detail.Is(DebugInfo.Descriptor))
            .Unpack<DebugInfo>()
            .Detail.Should()
            .Contain("AwaitUnexpectedException");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; }
    }
}

internal static class GrpcErrorInterceptorTestExtensions
{
    public static async Task<Empty> AwaitUnexpectedException(this ArkGrpcErrorInterceptor interceptor)
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException("grpc exception detail");
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        return await interceptor.UnaryServerHandler(
            new Empty(),
            new TestServerCallContext(),
            (_, _) => Task.FromException<Empty>(exception)).ConfigureAwait(false);
    }
}

internal sealed class TestServerCallContext : ServerCallContext
{
    private Status _status;

    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "localhost";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore => new();
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore { get; } = new();
    protected override Status StatusCore
    {
        get => _status;
        set => _status = value;
    }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore =>
        new("test", new Dictionary<string, List<AuthProperty>>(StringComparer.Ordinal));
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    protected override IDictionary<object, object> UserStateCore { get; } = new Dictionary<object, object>();
}
