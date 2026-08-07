// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.Hosting.Contracts;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Retry;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using MessagePack.Resolvers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Server.Kestrel.Core;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using NodaTime;

using System.Collections.Concurrent;
using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>
/// Owns the synthetic mediator container and independently built transport hosts.
/// </summary>
public sealed class HostingTestFixture : IAsyncDisposable
{
    private readonly InMemNetwork _network = new();
    private readonly List<WebApplication> _hosts = [];
    private readonly List<Container> _hostContainers = [];
    private bool _rebusConfigured;
    private bool _disposed;

    /// <summary>Initializes a fixture with deterministic handlers and test-only identity.</summary>
    public HostingTestFixture()
    {
        Container = new Container();
        Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        State = new HostingTestState();
        PrincipalProvider = new TestPrincipalProvider();

        RegisterHandlers(Container);

        Container.RegisterAuthorization();
        Container.RegisterAuthorizationPolicy<HostingScopePolicy>();
        HostingEndpointMappings.RegisterRebusHandlers(Container);
        Container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));
    }

    internal sealed class HostingDeferredCommandHandler : ICommandHandler<HostingDeferredCommand>
    {
        private readonly HostingTestState _state;

        public HostingDeferredCommandHandler(HostingTestState state)
        {
            _state = state;
        }

        public async Task ExecuteAsync(HostingDeferredCommand command, CancellationToken ctk = default)
        {
            await (_state.Bus ?? throw new InvalidOperationException("The Rebus bus was not initialized."))
                .Advanced.TransportMessage.Defer(TimeSpan.FromHours(1)).ConfigureAwait(false);
            _state.DeferredMessages++;
        }
    }

    /// <summary>Gets the SimpleInjector container used by all synthetic hosts.</summary>
    public Container Container { get; }

    /// <summary>Gets deterministic state updated by synthetic handlers.</summary>
    public HostingTestState State { get; }

    /// <summary>Gets the mutable test-only principal provider used by the HTTP host.</summary>
    internal TestPrincipalProvider PrincipalProvider { get; }

    /// <summary>Gets whether the fixture has disposed its hosts and container.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Gets the shared in-memory network used by the Rebus host.</summary>
    public InMemNetwork Network => _network;

    /// <summary>
    /// Waits until every non-error queue in the in-memory network is empty, or throws
    /// <see cref="TimeoutException"/> if <paramref name="timeout"/> elapses first.
    /// </summary>
    public async Task WaitForIdleAsync(
        TimeSpan? timeout = null,
        bool ignoreDeferred = false,
        CancellationToken ctk = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));

        try
        {
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                var pending = GetRebusCounts();

                if (pending.InQueue + pending.InProcess + (ignoreDeferred ? 0 : pending.Deferred) == 0)
                    return;

                await Task.Delay(50, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ctk.IsCancellationRequested)
        {
            var counts = GetRebusCounts();
            throw new TimeoutException(
                $"Rebus did not become idle. queue={counts.InQueue}, in-process={counts.InProcess}, deferred={counts.Deferred}, outbox={counts.Outbox}, error={counts.Error}.");
        }
    }

    /// <summary>Gets diagnostic counts for all synthetic Rebus work.</summary>
    public RebusWorkCounts GetRebusCounts()
    {
        var queues = _network.Queues.ToArray();
        var inQueue = _network.GetCount("hosting");
        var error = queues
            .Where(queue => string.Equals(queue, "hosting-error", StringComparison.OrdinalIgnoreCase))
            .Sum(queue => _network.GetCount(queue));

        return new RebusWorkCounts(
            inQueue,
            InProcessMessageInspectorStep.Count,
            TestsInMemoryTimeoutManager.DueCount,
            0,
            error);
    }

    /// <summary>Builds and maps an independent Minimal API host.</summary>
    /// <returns>The unstarted Minimal API application.</returns>
    public WebApplication BuildMinimalApiHost()
    {
        ThrowIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Container);
        builder.Services.AddSingleton(PrincipalProvider);
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                static _ => { });
        builder.Services.AddArkMinimalApiHost(Container, options =>
        {
            options.RequireAuthenticatedUser = false;
            options.CrossWireContainer = (container, services) =>
                container.RegisterInstance(services.GetRequiredService<IHttpContextAccessor>());
        });
        builder.Services.AddArkProblemDetailsExceptionHandler();
        builder.Services.AddMessagePackFormatter(StandardResolver.Instance);
        builder.Services.AddOpenApi("v1", ConfigureOpenApi);
        builder.Services.AddOpenApi("v2", ConfigureOpenApi);
        var app = builder.Build();
        app.UseArkProblemDetailsExceptionHandler();
        app.UseArkMinimalApiHost(Container);
        app.Use(async (context, next) =>
        {
            var sizeLimit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
            if (sizeLimit is not null && context.Request.ContentLength > sizeLimit)
            {
                context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                return;
            }

            await next().ConfigureAwait(false);
        });
        HostingEndpointMappings.MapMinimalApi(app);
        app.MapOpenApi().AllowAnonymous();
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds and starts an independent Minimal API test host.</summary>
    /// <returns>The started Minimal API application.</returns>
    public async Task<WebApplication> StartMinimalApiHostAsync()
    {
        var app = BuildMinimalApiHost();
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds and maps an independent code-first gRPC host.</summary>
    /// <param name="listenAddress">Optional Kestrel address; null selects TestServer.</param>
    /// <returns>The unstarted gRPC application.</returns>
    public WebApplication BuildGrpcHost(Uri? listenAddress = null)
    {
        ThrowIfDisposed();
        var builder = WebApplication.CreateBuilder();
        if (listenAddress is null)
            builder.WebHost.UseTestServer();
        else
            builder.WebHost.UseKestrel(options =>
                options.Listen(
                    System.Net.IPAddress.Loopback,
                    listenAddress.Port,
                    listenOptions => listenOptions.Protocols = HttpProtocols.Http2));
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                static _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
        builder.Services.AddCodeFirstGrpcReflection();
        var container = CreateHostContainer();
        _hostContainers.Add(container);
        builder.Services.AddSingleton(container);
        builder.Services.AddSingleton(PrincipalProvider);
        builder.Services.AddSimpleInjector(container, simpleInjector => simpleInjector.AddAspNetCore());
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ((IApplicationBuilder)app).UseSimpleInjector(container);
        RuntimeTypeModel.Default.AddNodaTimeSurrogates();
        HostingEndpointMappings.MapGrpc(app);
        app.MapCodeFirstGrpcReflectionService().AllowAnonymous();
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds and starts an independent gRPC test host.</summary>
    /// <returns>The started gRPC application.</returns>
    public async Task<WebApplication> StartGrpcHostAsync()
    {
        var app = BuildGrpcHost();
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds and starts a gRPC host on a loopback Kestrel address for external tools.</summary>
    /// <param name="port">The loopback TCP port.</param>
    /// <returns>The started gRPC application.</returns>
    public async Task<WebApplication> StartGrpcKestrelHostAsync(int port)
    {
        var app = BuildGrpcHost(new Uri($"http://127.0.0.1:{port}", UriKind.Absolute));
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds an isolated in-memory Rebus bus for the synthetic messages.</summary>
    /// <returns>The started Rebus bus.</returns>
    public IBus BuildRebusHost()
    {
        ThrowIfDisposed();
        if (!_rebusConfigured)
        {
            Container.ConfigureRebus(config =>
            {
                config.Transport(transport => transport.UseInMemoryTransport(_network, "hosting"));
                config.Routing(HostingEndpointMappings.ConfigureRebusRouting);
                config.Options(options =>
                {
                    options.AddInProcessMessageInspector();
                    options.AutomaticallyFlowUserContext(Container);
                    options.ArkRetryStrategy(errorQueueName: "hosting-error", maxDeliveryAttempts: 2);
                });
                config.Timeouts(timeouts => timeouts.StoreInMemoryTests());
            });
            _rebusConfigured = true;
        }

        var bus = Container.GetInstance<IBus>();
        State.Bus = bus;
        return bus;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var index = _hosts.Count - 1; index >= 0; index--)
            await _hosts[index].DisposeAsync().ConfigureAwait(false);
        for (var index = _hostContainers.Count - 1; index >= 0; index--)
            await _hostContainers[index].DisposeAsync().ConfigureAwait(false);
        await Container.DisposeAsync().ConfigureAwait(false);
        TestsInMemoryTimeoutManager.ClearPendingDue();
        _network.Reset();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private Container CreateHostContainer()
    {
        var container = new Container
        {
            Options =
            {
                DefaultScopedLifestyle = new AsyncScopedLifestyle(),
            },
        };
        RegisterHandlers(container, includeRebusHandlers: false);
        container.RegisterAuthorization();
        container.RegisterAuthorizationPolicy<HostingScopePolicy>();
        return container;
    }

    private void RegisterHandlers(Container container, bool includeRebusHandlers = true)
    {
        container.RegisterInstance(State);
        container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(PrincipalProvider);
        container.Register<IRequestHandler<HostingRequest, HostingResponse>, HostingRequestHandler>();
        container.Register<IQueryHandler<HostingQuery, HostingResponse>, HostingQueryHandler>();
        container.Register<ICommandHandler<HostingCommand>, HostingCommandHandler>();
        if (includeRebusHandlers)
        {
            container.Register<ICommandHandler<HostingRebusCommand>, HostingRebusCommandHandler>();
            container.Register<ICommandHandler<HostingRetryCommand>, HostingRetryCommandHandler>();
            container.Register<ICommandHandler<HostingCancellationCommand>, HostingCancellationCommandHandler>();
            container.Register<ICommandHandler<HostingDeferredCommand>, HostingDeferredCommandHandler>();
            container.Register<HostingRebusScope>(Lifestyle.Scoped);
        }
        container.Register<IRequestHandler<HostingValidationRequest, HostingResponse>, HostingValidationHandler>();
        container.Register<IRequestHandler<HostingStatusRequest, HostingResponse>, HostingStatusHandler>();
        container.Register<IQueryHandler<HostingNotFoundQuery, HostingResponse>, HostingNotFoundHandler>();
        container.Register<IRequestHandler<HostingBusinessViolationRequest, HostingResponse>, HostingBusinessViolationHandler>();
        container.Register<IRequestHandler<HostingUnexpectedRequest, HostingResponse>, HostingUnexpectedHandler>();
        container.Register<IQueryHandler<HostingAuthorizedQuery, HostingResponse>, HostingAuthorizedHandler>();
        container.Register<IQueryHandler<HostingUserContextQuery, HostingResponse>, HostingUserContextHandler>();
        container.Register<IRequestHandler<HostingETagMismatchRequest, HostingResponse>, HostingETagMismatchHandler>();
        container.Register<IRequestHandler<HostingOptimisticConcurrencyRequest, HostingResponse>, HostingOptimisticConcurrencyHandler>();
        container.Register<IQueryHandler<HostingStreamQuery, IAsyncEnumerable<HostingStreamItem>>, HostingStreamHandler>();
        container.Register<IRequestHandler<HostingAttachmentUploadRequest, HostingResponse>, HostingAttachmentUploadHandler>();
        container.Register<IRequestHandler<HostingAttachmentCollectionUploadRequest, HostingResponse>, HostingAttachmentCollectionUploadHandler>();
        container.Register<IQueryHandler<HostingAttachmentDownloadQuery, Ark.MediatorFramework.IArkAttachment>, HostingAttachmentDownloadHandler>();
        container.Register<IQueryHandler<HostingOpenApiQuery, HostingOpenApiResponse>, HostingOpenApiHandler>();
        container.Register<IQueryHandler<HostingWireTypesQuery, HostingWireTypesResponse>, HostingWireTypesHandler>();
        container.Register<IQueryHandler<HostingVersionedQuery, HostingResponse>, HostingVersionedHandler>();
    }

    private static void ConfigureOpenApi(OpenApiOptions options)
    {
        options
            .AddArkServerSetProperties()
            .AddArkXmlDocumentation()
            .AddArkNodaTimeSchemas()
            .AddArkPolymorphism<HostingShape, HostingShapeKind>(
                "kind",
                (HostingShapeKind.Circle, typeof(HostingCircle)));
    }
}

/// <summary>Counts synthetic Rebus work for bounded wait diagnostics.</summary>
public sealed record RebusWorkCounts(int InQueue, int InProcess, int Deferred, int Outbox, int Error);

/// <summary>Deterministic state shared by synthetic mediator handlers.</summary>
public sealed class HostingTestState
{
    private readonly TaskCompletionSource _commandExecution = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _secondCommandExecution = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the number of request handler executions.</summary>
    public int RequestExecutions { get; internal set; }

    /// <summary>Gets whether a generated request supplied a cancellable token.</summary>
    public bool RequestCancellationTokenWasCancelable { get; internal set; }

    /// <summary>Gets the server-owned stamp received by the request handler.</summary>
    public string? LastRequestServerStamp { get; internal set; }

    /// <summary>Gets the number of command handler executions.</summary>
    public int CommandExecutions { get; internal set; }

    /// <summary>Gets a task that completes when a command handler executes.</summary>
    public Task CommandExecuted => _commandExecution.Task;

    /// <summary>Gets a task that completes when two commands have executed.</summary>
    public Task SecondCommandExecuted => _secondCommandExecution.Task;

    /// <summary>Gets the number of failed retry handler attempts.</summary>
    public int RetryAttempts => Volatile.Read(ref _retryAttempts);

    /// <summary>Gets the cancellation status observed by the Rebus handler.</summary>
    public bool RebusCancellationTokenWasCancelable { get; internal set; }

    /// <summary>Gets the user identifier propagated through Rebus headers.</summary>
    public string? RebusUserId { get; internal set; }

    /// <summary>Gets the number of deferred messages scheduled by handlers.</summary>
    public int DeferredMessages { get; internal set; }

    internal IBus? Bus { get; set; }

    /// <summary>Gets the scope identifiers observed by Rebus handlers.</summary>
    public ConcurrentBag<Guid> RebusScopeIds { get; } = [];

    internal void RecordRetryAttempt()
    {
        Interlocked.Increment(ref _retryAttempts);
    }

    private int _retryAttempts;

    /// <summary>Gets the name of the last uploaded attachment.</summary>
    public string? LastAttachmentName { get; internal set; }

    /// <summary>Gets the content read by the last single-file upload handler.</summary>
    public string? LastAttachmentContent { get; internal set; }

    /// <summary>Gets the number of files received by the last collection upload handler.</summary>
    public int LastAttachmentCount { get; internal set; }

    /// <summary>Gets the number of times an authorized handler executed.</summary>
    public int AuthorizedExecutions { get; internal set; }

    /// <summary>Gets or sets whether the stream producer waits after its first item.</summary>
    public bool HoldStreamAfterFirst { get; set; }

    /// <summary>Gets a task that completes when the stream producer yields its first item.</summary>
    public Task StreamFirstItemProduced => _streamFirstItem.Task;

    /// <summary>Gets a task that completes when stream cancellation reaches the producer.</summary>
    public Task StreamCancellationObserved => _streamCancellation.Task;

    private readonly TaskCompletionSource _streamFirstItem = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _streamRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _streamCancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void RecordCommandExecution()
    {
        CommandExecutions++;
        _commandExecution.TrySetResult();
        if (CommandExecutions >= 2)
            _secondCommandExecution.TrySetResult();
    }

    internal void RecordFirstStreamItem()
    {
        _streamFirstItem.TrySetResult();
    }

    internal void RecordStreamCancellation()
    {
        _streamCancellation.TrySetResult();
    }

    internal void ReleaseStream()
    {
        _streamRelease.TrySetResult();
    }

    internal Task WaitForStreamReleaseAsync(CancellationToken ctk)
    {
        return _streamRelease.Task.WaitAsync(ctk);
    }
}

internal static class HostingWireTypeValues
{
    internal static readonly LocalDate Date = new(2026, 8, 6);
    internal static readonly LocalDateTime DateTime = new(2026, 8, 6, 15, 44);
    internal const int CircleRadius = 7;
}

internal sealed class HostingRequestHandler : IRequestHandler<HostingRequest, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingRequestHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.RequestExecutions++;
        _state.RequestCancellationTokenWasCancelable = ctk.CanBeCanceled;
        _state.LastRequestServerStamp = request.ServerStamp;
        return new HostingResponse
        {
            Message = $"{request.Id}:{request.Filter}:{request.Value}",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingQueryHandler : IQueryHandler<HostingQuery, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = $"{query.Id}:{query.Value}",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingStatusHandler : IRequestHandler<HostingStatusRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingStatusRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = request.Value,
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingNotFoundHandler : IQueryHandler<HostingNotFoundQuery, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingNotFoundQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return null!;
    }
}

internal sealed class HostingCommandHandler : ICommandHandler<HostingCommand>
{
    private readonly HostingTestState _state;

    public HostingCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.RecordCommandExecution();
    }
}

internal sealed class HostingRebusCommandHandler : ICommandHandler<HostingRebusCommand>
{
    private readonly HostingTestState _state;
    private readonly HostingRebusScope _scope;

    public HostingRebusCommandHandler(
        HostingTestState state,
        HostingRebusScope scope)
    {
        _state = state;
        _scope = scope;
    }

    public async Task ExecuteAsync(HostingRebusCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.RebusScopeIds.Add(_scope.Id);
        _state.RebusUserId = MessageContext.Current?.IncomingStepContext?.Load<ClaimsPrincipal>()
            ?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _state.RebusCancellationTokenWasCancelable = ctk.CanBeCanceled;
        _state.RecordCommandExecution();
    }
}

internal sealed class HostingRebusScope
{
    internal Guid Id { get; } = Guid.NewGuid();
}

internal sealed class HostingRetryCommandHandler : ICommandHandler<HostingRetryCommand>
{
    private readonly HostingTestState _state;

    public HostingRetryCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingRetryCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.RecordRetryAttempt();
        throw new InvalidOperationException("Synthetic retry failure.");
    }
}

internal sealed class HostingCancellationCommandHandler : ICommandHandler<HostingCancellationCommand>
{
    private readonly HostingTestState _state;

    public HostingCancellationCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingCancellationCommand command, CancellationToken ctk = default)
    {
        _state.RebusCancellationTokenWasCancelable = ctk.CanBeCanceled;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

internal sealed class HostingValidationHandler : IRequestHandler<HostingValidationRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingValidationRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(nameof(HostingValidationRequest.Value), "The synthetic value is invalid."),
            ]);
    }
}

internal sealed class HostingBusinessViolationHandler : IRequestHandler<HostingBusinessViolationRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingBusinessViolationRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        var violation = new Ark.Tools.Core.BusinessRuleViolation.BusinessRuleViolation("Synthetic rule")
        {
            Detail = "The synthetic business rule was violated.",
            Status = 422,
        };
        throw new Ark.Tools.Core.BusinessRuleViolation.BusinessRuleViolationException(violation);
    }
}

internal sealed class HostingUnexpectedHandler : IRequestHandler<HostingUnexpectedRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingUnexpectedRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("The synthetic handler failed unexpectedly.");
    }
}

internal sealed class HostingAuthorizedHandler : IQueryHandler<HostingAuthorizedQuery, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingAuthorizedHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingAuthorizedQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.AuthorizedExecutions++;
        return new HostingResponse
        {
            Message = "authorized",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingUserContextHandler : IQueryHandler<HostingUserContextQuery, HostingResponse>
{
    private readonly IContextProvider<ClaimsPrincipal> _principalProvider;

    public HostingUserContextHandler(IContextProvider<ClaimsPrincipal> principalProvider)
    {
        _principalProvider = principalProvider;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingUserContextQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = _principalProvider.Current.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingETagMismatchHandler : IRequestHandler<HostingETagMismatchRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingETagMismatchRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new Ark.Tools.Core.EntityTag.EntityTagMismatchException("The synthetic ETag does not match.");
    }
}

internal sealed class HostingOptimisticConcurrencyHandler : IRequestHandler<HostingOptimisticConcurrencyRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(
        HostingOptimisticConcurrencyRequest request,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new Ark.Tools.Core.OptimisticConcurrencyException("The synthetic entity was concurrently modified.");
    }
}

internal sealed class HostingStreamHandler : IQueryHandler<HostingStreamQuery, IAsyncEnumerable<HostingStreamItem>>
{
    private readonly HostingTestState _state;

    public HostingStreamHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<IAsyncEnumerable<HostingStreamItem>> ExecuteAsync(HostingStreamQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return Enumerate(query.Count, _state, ctk);
    }

    private static async IAsyncEnumerable<HostingStreamItem> Enumerate(
        int count,
        HostingTestState state,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ctk)
    {
        for (var number = 1; number <= count; number++)
        {
            ctk.ThrowIfCancellationRequested();
            if (number == 1)
                state.RecordFirstStreamItem();
            yield return new HostingStreamItem { Number = number };
            if (number == 1 && state.HoldStreamAfterFirst)
            {
                try
                {
                    await state.WaitForStreamReleaseAsync(ctk).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    state.RecordStreamCancellation();
                    throw;
                }
            }
            await Task.Yield();
        }
    }
}

internal sealed class HostingAttachmentUploadHandler : IRequestHandler<HostingAttachmentUploadRequest, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingAttachmentUploadHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingAttachmentUploadRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (request.Attachment is not null)
        {
            using var reader = new StreamReader(request.Attachment.OpenRead(), Encoding.UTF8);
            _state.LastAttachmentContent = await reader.ReadToEndAsync(ctk).ConfigureAwait(false);
            _state.LastAttachmentName = request.Attachment.Name;
        }
        return new HostingResponse
        {
            Message = request.Attachment?.Name ?? "none",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingAttachmentCollectionUploadHandler : IRequestHandler<HostingAttachmentCollectionUploadRequest, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingAttachmentCollectionUploadHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingAttachmentCollectionUploadRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.LastAttachmentCount = request.Attachments.Count;
        return new HostingResponse
        {
            Message = string.Join(",", request.Attachments.Select(attachment => attachment.Name)),
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingAttachmentDownloadHandler : IQueryHandler<HostingAttachmentDownloadQuery, Ark.MediatorFramework.IArkAttachment>
{
    public async Task<Ark.MediatorFramework.IArkAttachment> ExecuteAsync(
        HostingAttachmentDownloadQuery query,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!string.Equals(query.Name, "download.txt", StringComparison.Ordinal))
            return null!;

        return new Ark.MediatorFramework.ArkAttachment(
            query.Name,
            "text/plain",
            () => new MemoryStream(Encoding.UTF8.GetBytes("downloaded content")));
    }
}

internal sealed class HostingOpenApiHandler : IQueryHandler<HostingOpenApiQuery, HostingOpenApiResponse>
{
    public async Task<HostingOpenApiResponse> ExecuteAsync(HostingOpenApiQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingOpenApiResponse
        {
            Date = new NodaTime.LocalDate(2026, 8, 6),
            Shape = new HostingCircle { Radius = 3 },
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingWireTypesHandler : IQueryHandler<HostingWireTypesQuery, HostingWireTypesResponse>
{
    public async Task<HostingWireTypesResponse> ExecuteAsync(
        HostingWireTypesQuery query,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingWireTypesResponse
        {
            Date = HostingWireTypeValues.Date,
            DateTime = HostingWireTypeValues.DateTime,
            Shape = new HostingCircle { Radius = HostingWireTypeValues.CircleRadius },
        };
    }
}

internal sealed class HostingVersionedHandler : IQueryHandler<HostingVersionedQuery, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingVersionedQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = $"{query.Id}:{query.Value ?? "versioned"}",
            ServerStamp = "hosting-server",
        };
    }
}
