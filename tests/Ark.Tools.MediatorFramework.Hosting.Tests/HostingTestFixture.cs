// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.Hosting.Contracts;
using Ark.Tools.MediatorFramework.Mcp;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Retry;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Solid.SimpleInjector;

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

using RebusBus = Rebus.Bus.IBus;
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
    private bool? _secondLevelRetriesEnabled;
    private bool _disposed;

    /// <summary>Initializes a fixture with deterministic handlers and test-only identity.</summary>
    public HostingTestFixture()
    {
        Container = new Container();
        Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        State = new HostingTestState();
        _principalProvider = new TestPrincipalProvider();

        _registerHandlers(Container);

        Container.RegisterAuthorization();
        Container.RegisterAuthorizationPolicy<HostingScopePolicy>();
        HostingEndpointMappings.RegisterRebusHandlers(Container);
        Container.Collection.Append<IHandleMessages<global::Rebus.Retry.Simple.IFailed<HostingSecondLevelRetryCommand>>, HostingSecondLevelRetryFailedHandler>();
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
            await (_state._bus ?? throw new InvalidOperationException("The Rebus bus was not initialized."))
                .Advanced.TransportMessage.Defer(TimeSpan.FromHours(1)).ConfigureAwait(false);
            _state._recordDeferredMessage();
        }
    }

    /// <summary>Gets the SimpleInjector container used by all synthetic hosts.</summary>
    public Container Container { get; }

    /// <summary>Gets deterministic state updated by synthetic handlers.</summary>
    public HostingTestState State { get; }

    /// <summary>Gets the mutable test-only principal provider used by the HTTP host.</summary>
    internal TestPrincipalProvider _principalProvider { get; }

    /// <summary>Gets whether the fixture has disposed its hosts and container.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Gets the shared in-memory network used by the Rebus host.</summary>
    public InMemNetwork Network => _network;

    /// <summary>
    /// Waits until every non-error queue in the in-memory network is empty and the error queue
    /// count is stable across five samples, or throws
    /// <see cref="TimeoutException"/> if <paramref name="timeout"/> elapses first.
    /// </summary>
    public async Task WaitForIdleAsync(
        TimeSpan? timeout = null,
        bool ignoreDeferred = false,
        CancellationToken ctk = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));
        var idleSamples = 0;
        var lastErrorCount = -1;

        try
        {
            while (idleSamples < 5)
            {
                cts.Token.ThrowIfCancellationRequested();

                var pending = GetRebusCounts();

                if (pending.InQueue + pending.InProcess + (ignoreDeferred ? 0 : pending.Deferred) == 0
                    && pending.Error == lastErrorCount)
                    idleSamples++;
                else
                    idleSamples = 0;

                lastErrorCount = pending.Error;
                await Task.Delay(50, cts.Token).ConfigureAwait(false);
            }
            return;
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
        var inQueue = queues
            .Where(static queue => !string.Equals(queue, "hosting-error", StringComparison.OrdinalIgnoreCase))
            .Sum(queue => _network.GetCount(queue));
        var error = queues
            .Where(static queue => string.Equals(queue, "hosting-error", StringComparison.OrdinalIgnoreCase))
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
        _throwIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Container);
        builder.Services.AddSingleton(_principalProvider);
        builder.Services
            .AddAuthentication(TestAuthenticationHandler._schemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler._schemeName,
                static _ => { });
        builder.Services.AddArkMinimalApiHost(Container, static options =>
        {
            options.RequireAuthenticatedUser = false;
            options.CrossWireContainer = static (container, services) =>
                container.RegisterInstance(services.GetRequiredService<IHttpContextAccessor>());
        });
        builder.Services.AddArkProblemDetailsExceptionHandler();
        builder.Services.AddMessagePackFormatter(StandardResolver.Instance);
        builder.Services.AddOpenApi("v1", _configureOpenApi);
        builder.Services.AddOpenApi("v2", _configureOpenApi);
        var app = builder.Build();
        app.UseArkProblemDetailsExceptionHandler();
        app.UseArkMinimalApiHost(Container);
        app.Use(static async (context, next) =>
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

    /// <summary>Builds and maps an independent MCP host.</summary>
    /// <returns>The unstarted MCP application.</returns>
    public WebApplication BuildMcpHost()
    {
        _throwIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Container);
        builder.Services.AddSingleton(_principalProvider);
        builder.Services.AddSimpleInjector(Container, static simpleInjector => simpleInjector.AddAspNetCore());
        builder.Services
            .AddAuthentication(TestAuthenticationHandler._schemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler._schemeName,
                static _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddMcpServer()
            .WithHttpTransport()
            .WithArkMcpTools<HostingMcpContext>();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ((IApplicationBuilder)app).UseSimpleInjector(Container);
        app.MapMcp("/mcp/v{version}");
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds and starts an independent MCP test host.</summary>
    /// <returns>The started MCP application.</returns>
    public async Task<WebApplication> StartMcpHostAsync()
    {
        var app = BuildMcpHost();
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds and maps an independent code-first gRPC host.</summary>
    /// <param name="listenAddress">Optional Kestrel address; null selects TestServer.</param>
    /// <returns>The unstarted gRPC application.</returns>
    public WebApplication BuildGrpcHost(Uri? listenAddress = null)
    {
        _throwIfDisposed();
        var builder = WebApplication.CreateBuilder();
        if (listenAddress is null)
            builder.WebHost.UseTestServer();
        else
            builder.WebHost.UseKestrel(options =>
                options.Listen(
                    System.Net.IPAddress.Any,
                    listenAddress.Port,
                    static listenOptions => listenOptions.Protocols = HttpProtocols.Http2));
        builder.Services
            .AddAuthentication(TestAuthenticationHandler._schemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler._schemeName,
                static _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCodeFirstGrpc(static options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
        builder.Services.AddCodeFirstGrpcReflection();
        var container = _createHostContainer();
        _hostContainers.Add(container);
        builder.Services.AddSingleton(container);
        builder.Services.AddSingleton(_principalProvider);
        builder.Services.AddSimpleInjector(container, static simpleInjector => simpleInjector.AddAspNetCore());
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

    /// <summary>Registers the in-memory Rebus configuration without resolving the bus.</summary>
    /// <param name="secondLevelRetriesEnabled">Whether failed messages should be dispatched as <see cref="global::Rebus.Retry.Simple.IFailed{TMessage}"/>.</param>
    public void ConfigureRebusHost(bool secondLevelRetriesEnabled = false)
    {
        _throwIfDisposed();
        if (_rebusConfigured)
        {
            if (_secondLevelRetriesEnabled != secondLevelRetriesEnabled)
                throw new InvalidOperationException("The Rebus host was already configured with a different second-level retry setting.");

            return;
        }

        _secondLevelRetriesEnabled = secondLevelRetriesEnabled;
        Container.ConfigureRebus(config =>
        {
            config.Transport(transport => transport.UseInMemoryTransport(_network, "hosting"));
            config.Routing(HostingEndpointMappings.ConfigureRebusRouting);
            config.Options(options =>
            {
                options.AddInProcessMessageInspector();
                options.AutomaticallyFlowUserContext(Container);
                options.ArkRetryStrategy(
                    errorQueueName: "hosting-error",
                    maxDeliveryAttempts: 2,
                    secondLevelRetriesEnabled: secondLevelRetriesEnabled);
            });
            config.Timeouts(static timeouts => timeouts.StoreInMemoryTests());
        });
        _rebusConfigured = true;
    }

    /// <summary>Builds an isolated in-memory Rebus bus for the synthetic messages.</summary>
    /// <param name="secondLevelRetriesEnabled">Whether failed messages should be dispatched as <see cref="global::Rebus.Retry.Simple.IFailed{TMessage}"/>.</param>
    /// <returns>The started Rebus bus.</returns>
    public RebusBus BuildRebusHost(bool secondLevelRetriesEnabled = false)
    {
        ConfigureRebusHost(secondLevelRetriesEnabled);

        var bus = Container.GetInstance<RebusBus>();
        State._bus = bus;
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

    private void _throwIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private Container _createHostContainer()
    {
        var container = new Container
        {
            Options =
            {
                DefaultScopedLifestyle = new AsyncScopedLifestyle(),
            },
        };
        _registerHandlers(container, includeRebusHandlers: false);
        container.RegisterAuthorization();
        container.RegisterAuthorizationPolicy<HostingScopePolicy>();
        return container;
    }

    private void _registerHandlers(Container container, bool includeRebusHandlers = true)
    {
        container.RegisterSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
        container.RegisterSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
        container.RegisterSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
        container.RegisterInstance(State);
        container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(_principalProvider);
        container.Register<IRequestHandler<HostingRequest, HostingResponse>, HostingRequestHandler>();
        container.Register<IQueryHandler<HostingQuery, HostingResponse>, HostingQueryHandler>();
        container.Register<ICommandHandler<HostingCommand>, HostingCommandHandler>();
        if (includeRebusHandlers)
        {
            container.Register<ICommandHandler<HostingBusCommand>, HostingBusCommandHandler>();
            container.Register<ICommandHandler<HostingRebusCommand>, HostingRebusCommandHandler>();
            container.Register<ICommandHandler<HostingRetryCommand>, HostingRetryCommandHandler>();
            container.Register<ICommandHandler<HostingSecondLevelRetryCommand>, HostingSecondLevelRetryCommandHandler>();
            container.Register<ICommandHandler<HostingCancellationCommand>, HostingCancellationCommandHandler>();
            container.Register<ICommandHandler<HostingDeferredCommand>, HostingDeferredCommandHandler>();
            container.Register<HostingRebusScope>(Lifestyle.Scoped);
        }
        container.Register<IRequestHandler<HostingValidationRequest, HostingResponse>, HostingValidationHandler>();
        container.Register<IRequestHandler<HostingStatusRequest, HostingResponse>, HostingStatusHandler>();
        container.Register<IRequestHandler<HostingNoContentRequest, HostingResponse>, HostingNoContentHandler>();
        container.Register<IQueryHandler<HostingETagQuery, HostingETagResponse>, HostingETagQueryHandler>();
        container.Register<IRequestHandler<HostingETagUpdateRequest, HostingETagResponse>, HostingETagUpdateHandler>();
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
        container.Register<IQueryHandler<HostingAttachmentDownloadQuery, Ark.Tools.MediatorFramework.IArkAttachment>, HostingAttachmentDownloadHandler>();
        container.Register<IQueryHandler<HostingOpenApiQuery, HostingOpenApiResponse>, HostingOpenApiHandler>();
        container.Register<IQueryHandler<HostingWireTypesQuery, HostingWireTypesResponse>, HostingWireTypesHandler>();
        container.Register<IQueryHandler<HostingVersionedQuery, HostingResponse>, HostingVersionedHandler>();
    }

    private static void _configureOpenApi(OpenApiOptions options)
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
    public int CommandExecutions => Volatile.Read(ref _commandExecutions);

    /// <summary>Gets a task that completes when a command handler executes.</summary>
    public Task CommandExecuted => _commandExecution.Task;

    /// <summary>Gets a task that completes when two commands have executed.</summary>
    public Task SecondCommandExecuted => _secondCommandExecution.Task;

    /// <summary>Gets the number of bus-dispatched command handler executions.</summary>
    public int BusCommandExecutions => Volatile.Read(ref _busCommandExecutions);

    /// <summary>Gets a task that completes when a bus-dispatched command handler executes.</summary>
    public Task BusCommandExecuted => _busCommandExecution.Task;

    /// <summary>Gets the last bus-dispatched command value observed by the handler.</summary>
    public string? LastBusCommandValue => Volatile.Read(ref _lastBusCommandValue);

    /// <summary>Gets the ETag received by the last ETag update handler execution.</summary>
    public string? LastETagReceived => Volatile.Read(ref _lastETagReceived);

    private readonly TaskCompletionSource _busCommandExecution = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _busCommandExecutions;
    private string? _lastBusCommandValue;
    private string? _lastETagReceived;

    internal void _recordBusCommandExecution(string value)
    {
        Interlocked.Increment(ref _busCommandExecutions);
        Interlocked.Exchange(ref _lastBusCommandValue, value);
        _busCommandExecution.TrySetResult();
    }

    internal void _recordETagReceived(string? etag)
    {
        Interlocked.Exchange(ref _lastETagReceived, etag);
    }

    /// <summary>Gets the number of failed retry handler attempts.</summary>
    public int RetryAttempts => Volatile.Read(ref _retryAttempts);

    /// <summary>Gets the number of second-level retry handler attempts.</summary>
    public int SecondLevelRetryAttempts => Volatile.Read(ref _secondLevelRetryAttempts);

    /// <summary>Gets the number of failed-message handler executions.</summary>
    public int FailedMessageExecutions => Volatile.Read(ref _failedMessageExecutions);

    /// <summary>Gets or sets whether the failed-message handler should throw.</summary>
    public bool FailSecondLevelRetryHandler
    {
        get => Volatile.Read(ref _failSecondLevelRetryHandler);
        set => Volatile.Write(ref _failSecondLevelRetryHandler, value);
    }

    /// <summary>Gets the exception message supplied to the failed-message handler.</summary>
    public string? FailedMessageException => Volatile.Read(ref _failedMessageException);

    /// <summary>Gets the cancellation status observed by the Rebus handler.</summary>
    public bool RebusCancellationTokenWasCancelable => Volatile.Read(ref _rebusCancellationTokenWasCancelable);

    /// <summary>Gets the user identifier propagated through Rebus headers.</summary>
    public string? RebusUserId => Volatile.Read(ref _rebusUserId);

    /// <summary>Gets the number of deferred messages scheduled by handlers.</summary>
    public int DeferredMessages => Volatile.Read(ref _deferredMessages);

    internal RebusBus? _bus { get; set; }

    /// <summary>Gets the scope identifiers observed by Rebus handlers.</summary>
    public ConcurrentBag<Guid> RebusScopeIds { get; } = [];

    internal void _recordRetryAttempt()
    {
        Interlocked.Increment(ref _retryAttempts);
    }

    private int _retryAttempts;
    private int _secondLevelRetryAttempts;
    private int _failedMessageExecutions;
    private string? _failedMessageException;
    private bool _rebusCancellationTokenWasCancelable;
    private string? _rebusUserId;
    private int _deferredMessages;
    private bool _failSecondLevelRetryHandler;

    internal void _recordSecondLevelRetryAttempt()
    {
        Interlocked.Increment(ref _secondLevelRetryAttempts);
    }

    internal void _recordFailedMessage(global::Rebus.Retry.Simple.IFailed<HostingSecondLevelRetryCommand> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Interlocked.Increment(ref _failedMessageExecutions);
        Interlocked.Exchange(ref _failedMessageException, message.Exceptions?.FirstOrDefault()?.Message);
    }

    internal void _recordDeferredMessage()
    {
        Interlocked.Increment(ref _deferredMessages);
    }

    internal void _recordRebusUserId(string? userId)
    {
        Interlocked.Exchange(ref _rebusUserId, userId);
    }

    internal void _recordRebusCancellationToken(bool canBeCanceled)
    {
        Volatile.Write(ref _rebusCancellationTokenWasCancelable, canBeCanceled);
    }

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
    private int _commandExecutions;

    internal void _recordCommandExecution()
    {
        var executions = Interlocked.Increment(ref _commandExecutions);
        _commandExecution.TrySetResult();
        if (executions >= 2)
            _secondCommandExecution.TrySetResult();
    }

    internal void _recordFirstStreamItem()
    {
        _streamFirstItem.TrySetResult();
    }

    internal void _recordStreamCancellation()
    {
        _streamCancellation.TrySetResult();
    }

    internal void _releaseStream()
    {
        _streamRelease.TrySetResult();
    }

    internal Task _waitForStreamReleaseAsync(CancellationToken ctk)
    {
        return _streamRelease.Task.WaitAsync(ctk);
    }
}

internal static class HostingWireTypeValues
{
    public static readonly LocalDate Date = new(2026, 8, 6);
    public static readonly LocalDateTime DateTime = new(2026, 8, 6, 15, 44);
    public const int CircleRadius = 7;
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

internal sealed class HostingNoContentHandler : IRequestHandler<HostingNoContentRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingNoContentRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return null!;
    }
}

internal sealed class HostingBusCommandHandler : ICommandHandler<HostingBusCommand>
{
    private readonly HostingTestState _state;

    public HostingBusCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingBusCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state._recordBusCommandExecution(command.Value);
    }
}

internal sealed class HostingETagQueryHandler : IQueryHandler<HostingETagQuery, HostingETagResponse>
{
    public async Task<HostingETagResponse> ExecuteAsync(HostingETagQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingETagResponse { Token = "hosting-v2" };
    }
}

internal sealed class HostingETagUpdateHandler : IRequestHandler<HostingETagUpdateRequest, HostingETagResponse>
{
    private readonly HostingTestState _state;

    public HostingETagUpdateHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingETagResponse> ExecuteAsync(HostingETagUpdateRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state._recordETagReceived(request.ETag);
        return new HostingETagResponse { Token = "hosting-v3", ReceivedETag = request.ETag };
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
        _state._recordCommandExecution();
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
        _state._recordRebusUserId(MessageContext.Current?.IncomingStepContext?.Load<ClaimsPrincipal>()
            ?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        _state._recordRebusCancellationToken(ctk.CanBeCanceled);
        _state._recordCommandExecution();
    }
}

internal sealed class HostingRebusScope
{
    public Guid Id { get; } = Guid.NewGuid();
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
        _state._recordRetryAttempt();
        throw new InvalidOperationException("Synthetic retry failure.");
    }
}

internal sealed class HostingSecondLevelRetryCommandHandler : ICommandHandler<HostingSecondLevelRetryCommand>
{
    private readonly HostingTestState _state;

    public HostingSecondLevelRetryCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingSecondLevelRetryCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state._recordSecondLevelRetryAttempt();
        throw new InvalidOperationException("Synthetic second-level retry failure.");
    }
}

internal sealed class HostingSecondLevelRetryFailedHandler : IHandleMessages<global::Rebus.Retry.Simple.IFailed<HostingSecondLevelRetryCommand>>
{
    private readonly HostingTestState _state;

    public HostingSecondLevelRetryFailedHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task Handle(global::Rebus.Retry.Simple.IFailed<HostingSecondLevelRetryCommand> message)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state._recordFailedMessage(message);
        if (_state.FailSecondLevelRetryHandler)
            throw new InvalidOperationException("Synthetic second-level retry handler failure.");
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
        _state._recordRebusCancellationToken(ctk.CanBeCanceled);
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
        var violation = new Core.BusinessRuleViolation.BusinessRuleViolation("Synthetic rule")
        {
            Detail = "The synthetic business rule was violated.",
            Status = 422,
        };
        throw new Core.BusinessRuleViolation.BusinessRuleViolationException(violation);
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
        throw new Core.EntityTag.EntityTagMismatchException("The synthetic ETag does not match.");
    }
}

internal sealed class HostingOptimisticConcurrencyHandler : IRequestHandler<HostingOptimisticConcurrencyRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(
        HostingOptimisticConcurrencyRequest request,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new Core.OptimisticConcurrencyException("The synthetic entity was concurrently modified.");
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
        return _enumerate(query.Count, _state, ctk);
    }

    private static async IAsyncEnumerable<HostingStreamItem> _enumerate(
        int count,
        HostingTestState state,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ctk)
    {
        for (var number = 1; number <= count; number++)
        {
            ctk.ThrowIfCancellationRequested();
            if (number == 1)
                state._recordFirstStreamItem();
            yield return new HostingStreamItem { Number = number };
            if (number == 1 && state.HoldStreamAfterFirst)
            {
                try
                {
                    await state._waitForStreamReleaseAsync(ctk).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    state._recordStreamCancellation();
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
            Message = string.Join(",", request.Attachments.Select(static attachment => attachment.Name)),
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingAttachmentDownloadHandler : IQueryHandler<HostingAttachmentDownloadQuery, Ark.Tools.MediatorFramework.IArkAttachment>
{
    private readonly HostingTestState _state;

    public HostingAttachmentDownloadHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<Ark.Tools.MediatorFramework.IArkAttachment> ExecuteAsync(
        HostingAttachmentDownloadQuery query,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (string.Equals(query.Name, _state.LastAttachmentName, StringComparison.Ordinal)
            && _state.LastAttachmentContent is not null)
        {
            var content = _state.LastAttachmentContent;
            return new Ark.Tools.MediatorFramework.ArkAttachment(
                query.Name,
                "text/plain",
                () => new MemoryStream(Encoding.UTF8.GetBytes(content)));
        }

        if (!string.Equals(query.Name, "download.txt", StringComparison.Ordinal))
            return null!;

        return new Ark.Tools.MediatorFramework.ArkAttachment(
            query.Name,
            "text/plain",
            static () => new MemoryStream(Encoding.UTF8.GetBytes("downloaded content")));
    }
}

internal sealed class HostingOpenApiHandler : IQueryHandler<HostingOpenApiQuery, HostingOpenApiResponse>
{
    public async Task<HostingOpenApiResponse> ExecuteAsync(HostingOpenApiQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingOpenApiResponse
        {
            Date = new LocalDate(2026, 8, 6),
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
