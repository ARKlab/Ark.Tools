// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Core.BusinessRuleViolation;

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

    [TestMethod]
    public async Task MapsConcurrencyExceptions()
    {
        var interceptor = new ArkGrpcErrorInterceptor();

        Func<Task> etagAction = () => interceptor.UnaryServerHandler(
            new Empty(),
            new TestServerCallContext(),
            (_, _) => Task.FromException<Empty>(new EntityTagMismatchException("etag")));
        var etag = await etagAction.Should().ThrowAsync<RpcException>();
        etag.Which.StatusCode.Should().Be(StatusCode.FailedPrecondition);

        Func<Task> optimisticAction = () => interceptor.UnaryServerHandler(
            new Empty(),
            new TestServerCallContext(),
            (_, _) => Task.FromException<Empty>(new OptimisticConcurrencyException("conflict")));
        var optimistic = await optimisticAction.Should().ThrowAsync<RpcException>();
        optimistic.Which.StatusCode.Should().Be(StatusCode.Aborted);
    }

    [TestMethod]
    public async Task RethrowsOperationCanceledExceptionWhenClientCancelled()
    {
        var interceptor = new ArkGrpcErrorInterceptor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var context = new TestServerCallContext(cts.Token);

        Func<Task> action = () => interceptor.UnaryServerHandler(
            new Empty(),
            context,
            (_, _) => Task.FromException<Empty>(new OperationCanceledException(cts.Token)));

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task RethrowsRpcExceptionWithoutChangingStackTrace()
    {
        var interceptor = new ArkGrpcErrorInterceptor();
        Exception sourceException;
        try
        {
            throw new RpcException(new Status(StatusCode.Aborted, "aborted"));
        }
        catch (Exception exception)
        {
            sourceException = exception;
        }

        Func<Task> action = () => interceptor.UnaryServerHandler(
            new Empty(),
            new TestServerCallContext(),
            (_, _) => Task.FromException<Empty>(sourceException));

        var caught = await action.Should().ThrowAsync<RpcException>();
        caught.Which.StackTrace.Should().Be(sourceException.StackTrace);
    }

    [TestMethod]
    public async Task MapsInternalTimeoutOperationCanceledExceptionToInternalError()
    {
        var interceptor = new ArkGrpcErrorInterceptor(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new ArkGrpcErrorOptions()));

        // CancellationToken.None means IsCancellationRequested = false → internal timeout scenario
        Func<Task> action = () => interceptor.UnaryServerHandler(
            new Empty(),
            new TestServerCallContext(CancellationToken.None),
            (_, _) => Task.FromException<Empty>(new OperationCanceledException()));

        var exception = await action.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.Internal);
    }

    [TestMethod]
    public async Task MapsPublicBusinessRuleExtensions()
    {
        var interceptor = new ArkGrpcErrorInterceptor();
        var violation = new TestBusinessRuleViolation
        {
            Exposed = "visible",
            Additional = "additional",
        };

        Func<Task> action = () => interceptor.UnaryServerHandler(
            new Empty(),
            new TestServerCallContext(),
            (_, _) => Task.FromException<Empty>(new BusinessRuleViolationException(violation)));

        var exception = await action.Should().ThrowAsync<RpcException>();
        var status = RpcStatus.Parser.ParseFrom(
            exception.Which.Trailers.GetValueBytes("grpc-status-details-bin"));
        var detail = status.Details.Single(item => item.Is(ArkBusinessRuleViolation.Descriptor))
            .Unpack<ArkBusinessRuleViolation>();

        detail.Extensions.Should().ContainKey(nameof(TestBusinessRuleViolation.Exposed));
        detail.Extensions.Should().ContainKey(nameof(TestBusinessRuleViolation.Additional));
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

internal sealed class TestBusinessRuleViolation : BusinessRuleViolation
{
    public TestBusinessRuleViolation()
        : base("test")
    {
    }

    public string? Exposed { get; set; }

    public string? Additional { get; set; }
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
    private readonly CancellationToken _cancellationToken;

    public TestServerCallContext(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
    }

    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "localhost";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore => new();
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
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
