// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.RebusProcessor;
using Ark.MediatorFramework.Sample.Tests.Fakes;

using Ark.Tools.Outbox;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using NodaTime;
using NodaTime.Testing;

using Rebus.Transport.InMem;
using Rebus.Bus;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>
/// Owns the application composition used by direct contract tests.
/// </summary>
public sealed class ApplicationTestContext : IAsyncDisposable
{
    private readonly AsyncLocal<Scope?> _currentScope = new();
    private readonly Container _container;
    private readonly TestPrincipalProvider _principalProvider;
    private readonly ScenarioBindingHolder<IPrintCompletedNotificationService> _printCompletedNotificationBinding;
    private readonly MockPrintCompletedNotificationService _printCompletedNotificationService;
    private readonly ScenarioPrintCompletedNotificationService _printCompletedNotificationProxy;
    private readonly bool _usesSqlStore;
    private readonly string? _connectionString;
    private bool _verified;
    private bool _busStarted;
    private bool _disposed;

    /// <summary>
    /// Initializes a scenario-owned application test context.
    /// </summary>
    /// <param name="useSqlStore">Whether to use the SQL-backed store.</param>
    /// <param name="connectionString">The optional SQL connection string.</param>
    /// <param name="dataContextFactory">The optional context factory shared with another test resource.</param>
    /// <param name="printCompletedNotificationService">The optional scenario-owned external-service mock.</param>
    public ApplicationTestContext(
        bool? useSqlStore = null,
        string? connectionString = null,
        ISampleDataContextFactory? dataContextFactory = null,
        MockPrintCompletedNotificationService? printCompletedNotificationService = null)
    {
        Network = new InMemNetwork();
        Clock = new FakeClock(Instant.FromUtc(2026, 7, 27, 12, 0));
        _principalProvider = new TestPrincipalProvider();
        _printCompletedNotificationService = printCompletedNotificationService ?? new MockPrintCompletedNotificationService();
        _printCompletedNotificationBinding = new ScenarioBindingHolder<IPrintCompletedNotificationService>();
        if (printCompletedNotificationService is null)
            _printCompletedNotificationBinding.Attach(_printCompletedNotificationService.Mock.Object);
        _printCompletedNotificationProxy = new ScenarioPrintCompletedNotificationService(_printCompletedNotificationBinding);
        _container = new Container
        {
            Options =
            {
                DefaultScopedLifestyle = new AsyncScopedLifestyle(),
            },
        };

        _usesSqlStore = useSqlStore ?? !string.Equals(
            Environment.GetEnvironmentVariable("ARK_SAMPLE_INMEMORY_TESTS"),
            "1",
            StringComparison.Ordinal);
        _connectionString = connectionString ?? Environment.GetEnvironmentVariable("ARK_SAMPLE_SQL_CONNECTION");
        ApplicationComposition.Register(
            _container,
            _usesSqlStore,
            _connectionString,
            Clock,
            dataContextFactory,
            _printCompletedNotificationProxy);
        _container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(_principalProvider);
        _container.RegisterAuthorization();
        _container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        _container.RegisterInstance(this);
        _container.Register<DispatchScopeMarker>(Lifestyle.Scoped);
        _container.RegisterSingleton<ScopedDisposalTracker>();
        _container.Register<ScopedDisposalResource>(Lifestyle.Scoped);
        _container.Register<IRequestHandler<ScopeProbeRequest, Guid>, ScopeProbeHandler>();
        _container.Register<IRequestHandler<NestedScopeRequest, ScopeObservation>, NestedScopeHandler>();
        _container.Register<IRequestHandler<FailingScopeRequest, bool>, FailingScopeHandler>();
        ApplicationComposition.RegisterOutboundRebus(
            _container,
            transport => transport.UseDrainableInMemoryTransportAsOneWayClient(Network),
            SampleRebusEndpoints.ConfigureRouting);
        SetAuthenticatedUser();
    }

    /// <summary>Gets the scenario-owned in-memory Rebus network.</summary>
    public InMemNetwork Network { get; }

    /// <summary>Gets the deterministic application clock.</summary>
    public FakeClock Clock { get; }

    /// <summary>Gets whether this scenario uses the SQL-backed persistence profile.</summary>
    public bool UsesSqlStore => _usesSqlStore;

    /// <summary>Gets the optional SQL Server connection-string override for this scenario.</summary>
    public string? ConnectionString => _connectionString;

    public IPrintCompletedNotificationService PrintCompletedNotificationService => _printCompletedNotificationProxy;

    public void AttachPrintCompletedNotificationService(IPrintCompletedNotificationService service)
    {
        _printCompletedNotificationBinding.Attach(service);
    }

    public void DetachPrintCompletedNotificationService()
    {
        _printCompletedNotificationBinding.Detach();
    }

    /// <summary>Gets the context factory shared by the sender and receiver.</summary>
    public ISampleDataContextFactory DataContextFactory
    {
        get
        {
            _verify();
            return _container.GetInstance<ISampleDataContextFactory>();
        }
    }

    /// <summary>Gets the in-memory context factory when this scenario uses in-memory persistence.</summary>
    public InMemorySampleDataContextFactory InMemoryDataContextFactory
    {
        get
        {
            _verify();
            return _container.GetInstance<ISampleDataContextFactory>()
                as InMemorySampleDataContextFactory
                ?? throw new InvalidOperationException("The scenario does not use in-memory persistence.");
        }
    }

    /// <summary>Gets the number of request handlers audited by the application graph.</summary>
    public int AuditCount
    {
        get
        {
            _verify();
            return _container.GetInstance<AuditCounter>().Count;
        }
    }

    /// <summary>Gets whether the failing dispatch released its scoped resource.</summary>
    public bool FailedDispatchResourceDisposed
    {
        get
        {
            _verify();
            return _container.GetInstance<ScopedDisposalTracker>().Disposed;
        }
    }

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
                ApplicationScopes.GreetingWrite,
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
            () => _container.GetInstance<IRequestHandler<TRequest, TResponse>>().ExecuteAsync(request, ctk))
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
            () => _container.GetInstance<IQueryHandler<TQuery, TResponse>>().ExecuteAsync(query, ctk))
            .ConfigureAwait(false);
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
            }).ConfigureAwait(false);
    }

    /// <summary>Starts the scenario-owned outbound Rebus client.</summary>
    public void StartOutboundBus()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _verify();
        if (_busStarted)
            return;

        _container.StartBus();
        _busStarted = true;
    }

    /// <summary>Sends a Rebus message through the scenario-owned outbound client.</summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task SendAsync<TMessage>(TMessage message, CancellationToken ctk = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        StartOutboundBus();
        await _container.GetInstance<IBus>().Send(message).ConfigureAwait(false);
    }

    /// <summary>Gets the number of pending outbox messages.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task<int> GetOutboxCountAsync(CancellationToken ctk = default)
    {
        _verify();
        var context = await _container.GetInstance<IOutboxAsyncContextFactory>()
            .CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var count = await context.CountAsync(ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return count;
    }

    public async Task<ISampleDataContext> CreateDataContextAsync(CancellationToken ctk = default)
    {
        _verify();
        return await _container.GetInstance<ISampleDataContextFactory>()
            .CreateAsync(ctk).ConfigureAwait(false);
    }

    /// <summary>Clears all pending outbox messages during scenario cleanup.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task ClearOutboxAsync(CancellationToken ctk = default)
    {
        _verify();
        var context = await _container.GetInstance<IOutboxAsyncContextFactory>()
            .CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        await context.ClearAsync(ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <summary>Disposes Rebus and all application resources owned by the context.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _printCompletedNotificationBinding.Detach();
        await _container.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task<TResponse> _dispatchAsync<TResponse>(Func<Task<TResponse>> execute)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _verify();

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

    private void _verify()
    {
        if (_verified)
            return;

        _container.Verify();
        _verified = true;
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
