// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Tests.Fakes;

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Sql.SqlServer;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>
/// Owns the application under test: the api process used for direct dispatch plus the worker, notification,
/// audit, and outbox-processor participants, each composed as its own process on a shared in-memory transport.
/// </summary>
public sealed class ApplicationTestContext : IAsyncDisposable
{
    private readonly AsyncLocal<Scope?> _currentScope = new();
    private readonly Lock _startGate = new();
    private readonly Container _container;
    private readonly TestPrincipalProvider _principalProvider;
    private readonly ScenarioBindingHolder<IPrintCompletedNotificationService> _printCompletedNotificationBinding;
    private readonly MockPrintCompletedNotificationService _printCompletedNotificationService;
    private readonly ScenarioPrintCompletedNotificationService _printCompletedNotificationProxy;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ISampleDataContextFactory _dataContextFactory;
    private readonly InMemoryMessagingTransport _transport = new();
    // The attachment lifetime must outlive the network's one-hour maximum scheduling delay.
    private readonly InMemoryMessagingDataBus _dataBus = new(SystemClock.Instance, Duration.FromHours(2));
    private readonly MessagingProcessingOptions _testProcessingOptions = new()
    {
        MinPollInterval = TimeSpan.FromMilliseconds(2),
        MaxPollInterval = TimeSpan.FromMilliseconds(200),
    };
    private readonly List<ParticipantProcess> _processes = [];
    private Task? _started;
    private ParticipantProcess? _api;
    private ParticipantProcess? _worker;
    private bool _disposed;

    /// <summary>
    /// Initializes a scenario-owned application test context.
    /// </summary>
    /// <param name="useSqlStore">Whether to use the SQL-backed store.</param>
    /// <param name="connectionString">The optional SQL connection string.</param>
    /// <param name="dataContextFactory">The optional context factory shared by every participant.</param>
    /// <param name="printCompletedNotificationService">The optional scenario-owned external-service mock.</param>
    public ApplicationTestContext(
        bool? useSqlStore = null,
        [InfrastructureSecret] string? connectionString = null,
        ISampleDataContextFactory? dataContextFactory = null,
        MockPrintCompletedNotificationService? printCompletedNotificationService = null)
    {
        Clock = new FakeClock(Instant.FromUtc(2026, 7, 27, 12, 0));
        _principalProvider = new TestPrincipalProvider();
        _printCompletedNotificationService = printCompletedNotificationService ?? new MockPrintCompletedNotificationService();
        _printCompletedNotificationBinding = new ScenarioBindingHolder<IPrintCompletedNotificationService>();
        if (printCompletedNotificationService is null)
            _printCompletedNotificationBinding.Attach(_printCompletedNotificationService.Mock.Object);
        _printCompletedNotificationProxy = new ScenarioPrintCompletedNotificationService(_printCompletedNotificationBinding);

        var usesSqlStore = useSqlStore ?? !string.Equals(
            Environment.GetEnvironmentVariable("ARK_SAMPLE_INMEMORY_TESTS"),
            "1",
            StringComparison.Ordinal);
        if (dataContextFactory is null && usesSqlStore)
        {
            var sqlConnectionString = connectionString ?? DatabaseHooks.ConnectionString;
            _applicationOptions = new ApplicationOptions
            {
                SqlConnectionString = sqlConnectionString,
                Clock = Clock,
                PrintCompletedNotificationService = _printCompletedNotificationProxy,
            };
            // One factory for seeding, outbox inspection, and the outbox processor; participants build their own.
            _dataContextFactory = new SampleDataContextFactory(
                new SqlConnectionManager(),
                new SampleDataContextConfig(sqlConnectionString));
        }
        else
        {
            _dataContextFactory = dataContextFactory
                ?? new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
            _applicationOptions = new ApplicationOptions
            {
                DataContextFactory = _dataContextFactory,
                Clock = Clock,
                PrintCompletedNotificationService = _printCompletedNotificationProxy,
            };
        }

        _container = _newContainer(_principalProvider);
        _container.RegisterInstance(this);
        _container.Register<DispatchScopeMarker>(Lifestyle.Scoped);
        _container.RegisterSingleton<ScopedDisposalTracker>();
        _container.Register<ScopedDisposalResource>(Lifestyle.Scoped);
        _container.Register<IRequestHandler<ScopeProbeRequest, Guid>, ScopeProbeHandler>();
        _container.Register<IRequestHandler<NestedScopeRequest, ScopeObservation>, NestedScopeHandler>();
        _container.Register<IRequestHandler<FailingScopeRequest, bool>, FailingScopeHandler>();
        SetAuthenticatedUser();
    }

    /// <summary>Gets the deterministic application clock.</summary>
    public FakeClock Clock { get; }

    /// <summary>Gets the shared in-memory transport.</summary>
    public InMemoryMessagingTransport Transport => _transport;

    /// <summary>Gets the notifications recorded by the notification subscriber.</summary>
    public RecordingBookPrintSink Notifications { get; } = new();

    /// <summary>Gets the audit records written by the audit subscriber.</summary>
    public RecordingBookPrintSink Audits { get; } = new();

    /// <summary>Gets the scenario-bound external print-completion service shared by every participant.</summary>
    public IPrintCompletedNotificationService PrintCompletedNotificationService => _printCompletedNotificationProxy;

    /// <summary>Binds the external print-completion service for the current scenario.</summary>
    /// <param name="service">The scenario-owned service.</param>
    public void AttachPrintCompletedNotificationService(IPrintCompletedNotificationService service)
    {
        _printCompletedNotificationBinding.Attach(service);
    }

    /// <summary>Unbinds the external print-completion service.</summary>
    public void DetachPrintCompletedNotificationService()
    {
        _printCompletedNotificationBinding.Detach();
    }

    /// <summary>Gets the number of request handlers audited by the api process.</summary>
    public int AuditCount => _startedContainer.GetInstance<AuditCounter>().Count;

    /// <summary>Gets whether the failing dispatch released its scoped resource.</summary>
    public bool FailedDispatchResourceDisposed => _startedContainer.GetInstance<ScopedDisposalTracker>().Disposed;

    /// <summary>Sets the principal used by authorization and auditing.</summary>
    /// <param name="principal">The principal for subsequent dispatches.</param>
    public void SetPrincipal(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        _principalProvider.SetCurrent(principal);
    }

    /// <summary>Sets an authenticated principal with the requested policy claims.</summary>
    /// <param name="subject">The authenticated subject.</param>
    /// <param name="scopes">The scope claims granted to the subject.</param>
    public void SetAuthenticatedUser(string subject = "application-test-user", params string[] scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(scopes);
        scopes = scopes.Length == 0
            ? [
                ApplicationScopes.BookRead,
                ApplicationScopes.BookWrite,
                ApplicationScopes.BookReviewsRead,
                ApplicationScopes.BookReviewsWrite,
                ApplicationScopes.BookActivityRead,
                ApplicationScopes.BookActivityWrite,
            ]
            : scopes;
        SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim("scope", string.Join(' ', scopes)),
            ],
            authenticationType: "application-test")));
    }

    /// <summary>Configures failures from the simulated external print-completion service.</summary>
    /// <param name="count">The number of notifications that should fail.</param>
    public void FailNextPrintCompletionNotifications(int count)
    {
        _printCompletedNotificationService.FailNext(count);
    }

    /// <summary>Verifies that the external print-completion service was called for a process.</summary>
    /// <param name="process">The expected process.</param>
    public void VerifyPrintCompletionNotification(BookPrintProcessResponse process)
    {
        _printCompletedNotificationService.VerifyNotification(process);
    }

    /// <summary>Starts the participants. Called before the first dispatch or send; later calls are no-ops.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task StartAsync(CancellationToken ctk = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Task started;
        lock (_startGate)
            started = _started ??= _startProcessesAsync(ctk);
        await started.ConfigureAwait(false);
    }

    /// <summary>Dispatches a request through its decorated application handler.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The handler response.</returns>
    public async Task<TResponse> DispatchRequestAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken ctk = default)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _dispatchAsync(
            () => _container.GetInstance<IRequestHandler<TRequest, TResponse>>().ExecuteAsync(request, ctk),
            ctk).ConfigureAwait(false);
    }

    /// <summary>Dispatches a request through its decorated application handler in the worker participant's container.</summary>
    /// <remarks>
    /// Print processing and its <c>BookPrintCompleted</c> publication are owned by the worker. Use this for
    /// worker-owned requests such as <c>ResumeBookPrintProcessRequest</c>.
    /// </remarks>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The handler response.</returns>
    public async Task<TResponse> DispatchWorkerRequestAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken ctk = default)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await StartAsync(ctk).ConfigureAwait(false);
        var scope = AsyncScopedLifestyle.BeginScope(_worker!.Container);
        await using var __scope = scope.ConfigureAwait(false);
        return await _worker.Container.GetInstance<IRequestHandler<TRequest, TResponse>>()
            .ExecuteAsync(request, ctk)
            .ConfigureAwait(false);
    }

    /// <summary>Dispatches a query through its decorated application handler.</summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="query">The query instance.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The handler response.</returns>
    public async Task<TResponse> DispatchQueryAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken ctk = default)
        where TQuery : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _dispatchAsync(
            () => _container.GetInstance<IQueryHandler<TQuery, TResponse>>().ExecuteAsync(query, ctk),
            ctk).ConfigureAwait(false);
    }

    /// <summary>Dispatches a command through its decorated application handler.</summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task DispatchCommandAsync<TCommand>(
        TCommand command,
        CancellationToken ctk = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        await _dispatchAsync(
            async () =>
            {
                await _container.GetInstance<ICommandHandler<TCommand>>()
                    .ExecuteAsync(command, ctk)
                    .ConfigureAwait(false);
                return true;
            },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Sends a message through the api process bus, as a host endpoint would.</summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task SendAsync<TMessage>(TMessage message, CancellationToken ctk = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        await StartAsync(ctk).ConfigureAwait(false);
        await _api!.Services.GetRequiredService<IBus>()
            .Send(message, cancellationToken: ctk)
            .ConfigureAwait(false);
    }

    /// <summary>Gets the number of pending outbox messages.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task<int> GetOutboxCountAsync(CancellationToken ctk = default)
    {
        var context = await _dataContextFactory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var count = await context.CountAsync(ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return count;
    }

    /// <summary>Seeds a running print process for a feature setup step.</summary>
    /// <param name="bookId">The book that owns the process.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The process that was stored.</returns>
    public async Task<BookPrintProcessResponse> SeedRunningBookPrintProcessAsync(
        BookId bookId,
        CancellationToken ctk = default)
    {
        if (!bookId.IsInitialized())
            throw new ArgumentException("A book identifier is required.", nameof(bookId));
        var process = new BookPrintProcessResponse
        {
            Id = BookPrintProcessId.New(),
            BookId = bookId,
            Progress = PrintProgress.From(0.5),
            Status = BookPrintProcessStatus.Running,
        };
        var context = await _dataContextFactory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        if (!await context.TrySaveBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
            throw new InvalidOperationException("The running book print process could not be seeded.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }

    /// <summary>Stops every participant, then releases the external-service binding.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_started is not null)
        {
            // Wait for a start in progress so no participant is left running; its failure is reported by the caller.
            _ = await Task.WhenAny(_started).ConfigureAwait(false);
        }

        for (var index = _processes.Count - 1; index >= 0; index--)
            await _processes[index].DisposeAsync().ConfigureAwait(false);
        if (_api is null)
            await _container.DisposeAsync().ConfigureAwait(false);
        _printCompletedNotificationBinding.Detach();
    }

    private Container _startedContainer => _api is not null
        ? _container
        : throw new InvalidOperationException("Dispatch through the context before reading the api process state.");

    private async Task _startProcessesAsync(CancellationToken ctk)
    {
        await InMemoryMessagingHarness.EnsureTopologyAsync(_transport, ctk).ConfigureAwait(false);
        _api = await _startParticipantAsync(
            _container,
            _principalProvider,
            static (b, t, d) => b.Producer<SampleMessagingApiParticipant>(p => p
                .UseTransport(t).UseDataBus(d).UseOutgoingPipeline(typeof(UserContextOutgoingStep)).UseOutbox()),
            ctk).ConfigureAwait(false);
        _processes.Add(_api);

        var workerPrincipal = new MessagePrincipalProvider();
        _worker = await _startParticipantAsync(
            _newContainer(workerPrincipal),
            workerPrincipal,
            static (b, t, d) => b.Receiver<SampleMessagingParticipant>(r => r
                .UseTransport(t).UseDataBus(d)
                .UseIncomingPipeline(typeof(UserContextIncomingStep)).UseOutgoingPipeline(typeof(UserContextOutgoingStep))
                .UseOutbox()),
            ctk).ConfigureAwait(false);
        _processes.Add(_worker);

        var notificationPrincipal = new MessagePrincipalProvider();
        var notification = _newContainer(notificationPrincipal);
        ApplicationComposition.RegisterNotificationSubscriber(notification, Notifications);
        _processes.Add(await _startParticipantAsync(
            notification,
            notificationPrincipal,
            static (b, t, d) => b.Receiver<SampleMessagingNotificationParticipant>(r => r
                .UseTransport(t).UseDataBus(d).UseIncomingPipeline(typeof(UserContextIncomingStep))),
            ctk).ConfigureAwait(false));

        var auditPrincipal = new MessagePrincipalProvider();
        var audit = _newContainer(auditPrincipal);
        ApplicationComposition.RegisterAuditSubscriber(audit, Audits);
        _processes.Add(await _startParticipantAsync(
            audit,
            auditPrincipal,
            static (b, t, d) => b.Receiver<SampleMessagingAuditParticipant>(r => r
                .UseTransport(t).UseDataBus(d).UseIncomingPipeline(typeof(UserContextIncomingStep))),
            ctk).ConfigureAwait(false));

        _processes.Add(await _startOutboxProcessorAsync(ctk).ConfigureAwait(false));
    }

    private Container _newContainer(IContextProvider<ClaimsPrincipal> principal)
    {
        var container = new Container { Options = { DefaultScopedLifestyle = new AsyncScopedLifestyle() } };
        ApplicationComposition.Register(container, _applicationOptions);
        container.RegisterInstance(principal);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        return container;
    }

    private async Task<ParticipantProcess> _startParticipantAsync(
        Container container,
        IContextProvider<ClaimsPrincipal> principal,
        Action<MessagingCompositionBuilder<SampleMessagingNetwork>, IMessagingTransport, IMessagingDataBus> select,
        CancellationToken ctk)
    {
        return await ParticipantProcess.StartAsync(
            container,
            services =>
            {
                InMemoryMessagingHarness.ConfigureJson(services);
                // Pipeline steps are resolved by type from the provider: register the instances first.
                services.AddSingleton(new UserContextOutgoingStep(() => principal.Current));
                if (principal is MessagePrincipalProvider receiverPrincipal)
                    services.AddSingleton(new UserContextIncomingStep(receiverPrincipal.Set));
                services.AddSingleton(_testProcessingOptions);
                services.ConfigureArkMessaging<SampleMessagingNetwork>(b => select(b, _transport, _dataBus));
            },
            ctk: ctk).ConfigureAwait(false);
    }

    private async Task<ParticipantProcess> _startOutboxProcessorAsync(CancellationToken ctk)
    {
        var container = new Container { Options = { DefaultScopedLifestyle = new AsyncScopedLifestyle() } };
        return await ParticipantProcess.StartAsync(
            container,
            services =>
            {
                services.AddSingleton<IMessagingTransport>(_transport);
                services.AddArkMessagingOutboxProcessor(_dataContextFactory, batchSize: 10);
            },
            bridgeBus: false,
            ctk: ctk).ConfigureAwait(false);
    }

    private async Task<TResponse> _dispatchAsync<TResponse>(Func<Task<TResponse>> execute, CancellationToken ctk)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await StartAsync(ctk).ConfigureAwait(false);

        var scope = _currentScope.Value;
        var ownsScope = scope is null;
        if (ownsScope)
        {
            scope = AsyncScopedLifestyle.BeginScope(_container);
            _currentScope.Value = scope;
        }

        try
        {
            return await execute().ConfigureAwait(false);
        }
        finally
        {
            if (ownsScope)
            {
                _currentScope.Value = null;
                await scope!.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class TestPrincipalProvider : IContextProvider<ClaimsPrincipal>
    {
        private ClaimsPrincipal _current = new(new ClaimsIdentity());

        public ClaimsPrincipal Current => _current;

        public void SetCurrent(ClaimsPrincipal principal)
        {
            _current = principal;
        }
    }
}

internal sealed class DispatchScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

internal sealed record ScopeProbeRequest : IRequest<ScopeProbeRequest, Guid>;

internal sealed record NestedScopeRequest : IRequest<NestedScopeRequest, ScopeObservation>;

internal sealed record ScopeObservation(Guid OuterScopeId, Guid NestedScopeId);

internal sealed record FailingScopeRequest : IRequest<FailingScopeRequest, bool>;

internal sealed class ScopeProbeHandler : IRequestHandler<ScopeProbeRequest, Guid>
{
    private readonly DispatchScopeMarker _marker;

    public ScopeProbeHandler(DispatchScopeMarker marker)
    {
        _marker = marker;
    }

    public async Task<Guid> ExecuteAsync(ScopeProbeRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return _marker.Id;
    }
}

internal sealed class NestedScopeHandler : IRequestHandler<NestedScopeRequest, ScopeObservation>
{
    private readonly ApplicationTestContext _context;
    private readonly DispatchScopeMarker _marker;

    public NestedScopeHandler(ApplicationTestContext context, DispatchScopeMarker marker)
    {
        _context = context;
        _marker = marker;
    }

    public async Task<ScopeObservation> ExecuteAsync(NestedScopeRequest request, CancellationToken ctk = default)
    {
        var nestedScopeId = await _context.DispatchRequestAsync<ScopeProbeRequest, Guid>(
            new ScopeProbeRequest(),
            ctk).ConfigureAwait(false);
        return new ScopeObservation(_marker.Id, nestedScopeId);
    }
}

internal sealed class ScopedDisposalTracker
{
    public bool Disposed { get; set; }
}

internal sealed class ScopedDisposalResource : IDisposable
{
    private readonly ScopedDisposalTracker _tracker;

    public ScopedDisposalResource(ScopedDisposalTracker tracker)
    {
        _tracker = tracker;
    }

    public void Dispose()
    {
        _tracker.Disposed = true;
    }
}

internal sealed class FailingScopeHandler : IRequestHandler<FailingScopeRequest, bool>
{
    public FailingScopeHandler(ScopedDisposalResource resource)
    {
        // Resolve the scoped resource so failed dispatch disposal is observable.
        _ = resource;
    }

    public async Task<bool> ExecuteAsync(FailingScopeRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("Synthetic dispatch failure.");
    }
}
