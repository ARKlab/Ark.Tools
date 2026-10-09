// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;
using Ark.Tools.Solid.Authorization;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

namespace Ark.Tools.Solid.SimpleInjector.Tests;

[TestClass]
public sealed class PolicyAuthorizeDecoratorTests
{
    [TestMethod]
    public async Task Denied_legacy_query_throws()
    {
        await _executeAsync(static async (q, _, _) => await q.ExecuteAsync(new DeniedLegacyQuery()).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Denied_self_generic_query_throws_on_both_dispatch_paths()
    {
        await _executeAsync(static async (q, _, _) => await q.ExecuteAsync(new DeniedSelfQuery()).ConfigureAwait(false)).ConfigureAwait(false);
        await _executeAsync(static async (q, _, _) => await q.ExecuteAsync<int>(new DeniedSelfQuery()).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Denied_legacy_request_throws()
    {
        await _executeAsync(static async (_, r, _) => await r.ExecuteAsync(new DeniedLegacyRequest()).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Denied_self_generic_request_throws_on_both_dispatch_paths()
    {
        await _executeAsync(static async (_, r, _) => await r.ExecuteAsync(new DeniedSelfRequest()).ConfigureAwait(false)).ConfigureAwait(false);
        await _executeAsync(static async (_, r, _) => await r.ExecuteAsync<int>(new DeniedSelfRequest()).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Denied_legacy_command_throws()
    {
        await _executeAsync(static async (_, _, c) => await c.ExecuteAsync(new DeniedLegacyCommand()).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Denied_self_generic_command_throws_on_both_dispatch_paths()
    {
        await _executeAsync(static async (_, _, c) => await c.ExecuteAsync(new DeniedSelfCommand()).ConfigureAwait(false)).ConfigureAwait(false);
        await _executeAsync(static async (_, _, c) => await c.ExecuteAsync((ICommand)new DeniedSelfCommand()).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Allowed_contracts_return_handler_result_for_both_contract_styles()
    {
        var container = _createContainer();
        await using var containerDisposal = container.ConfigureAwait(false);
        var scope = AsyncScopedLifestyle.BeginScope(container);
        await using var scopeDisposal = scope.ConfigureAwait(false);
        var queries = new SimpleInjectorQueryProcessor(container);
        var requests = new SimpleInjectorRequestProcessor(container);
        var commands = new SimpleInjectorCommandProcessor(container);
        var trace = container.GetInstance<Trace>();

        Assert.AreEqual(1, await queries.ExecuteAsync(new AllowedLegacyQuery()).ConfigureAwait(false));
        Assert.AreEqual(2, await queries.ExecuteAsync(new AllowedSelfQuery()).ConfigureAwait(false));
        Assert.AreEqual(2, await queries.ExecuteAsync<int>(new AllowedSelfQuery()).ConfigureAwait(false));
        Assert.AreEqual(3, await requests.ExecuteAsync(new AllowedLegacyRequest()).ConfigureAwait(false));
        Assert.AreEqual(4, await requests.ExecuteAsync(new AllowedSelfRequest()).ConfigureAwait(false));
        Assert.AreEqual(4, await requests.ExecuteAsync<int>(new AllowedSelfRequest()).ConfigureAwait(false));
        await commands.ExecuteAsync(new AllowedLegacyCommand()).ConfigureAwait(false);
        await commands.ExecuteAsync(new AllowedSelfCommand()).ConfigureAwait(false);
        await commands.ExecuteAsync((ICommand)new AllowedSelfCommand()).ConfigureAwait(false);

        Assert.AreEqual(3, trace.Commands);
    }

    private static async Task _executeAsync(Func<IQueryProcessor, IRequestProcessor, ICommandProcessor, Task> action)
    {
        var container = _createContainer();
        await using var containerDisposal = container.ConfigureAwait(false);
        var scope = AsyncScopedLifestyle.BeginScope(container);
        await using var scopeDisposal = scope.ConfigureAwait(false);
        var trace = container.GetInstance<Trace>();

        await Assert.ThrowsExactlyAsync<PolicyAuthorizationException>(
            async () => await action(
                new SimpleInjectorQueryProcessor(container),
                new SimpleInjectorRequestProcessor(container),
                new SimpleInjectorCommandProcessor(container)).ConfigureAwait(false)).ConfigureAwait(false);

        Assert.AreEqual(0, trace.Handled, "The handler must not run when the policy denies access.");
    }

    private static Container _createContainer()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.RegisterInstance(new Trace());
        container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new FixedPrincipalContextProvider(static () => new ClaimsPrincipal(new ClaimsIdentity())));
        container.RegisterAuthorization();

        container.Register<IQueryHandler<DeniedLegacyQuery, int>, QueryHandler<DeniedLegacyQuery>>();
        container.Register<IQueryHandler<DeniedSelfQuery, int>, QueryHandler<DeniedSelfQuery>>();
        container.Register<IQueryHandler<AllowedLegacyQuery, int>, QueryHandler<AllowedLegacyQuery>>();
        container.Register<IQueryHandler<AllowedSelfQuery, int>, QueryHandler<AllowedSelfQuery>>();
        container.Register<IRequestHandler<DeniedLegacyRequest, int>, RequestHandler<DeniedLegacyRequest>>();
        container.Register<IRequestHandler<DeniedSelfRequest, int>, RequestHandler<DeniedSelfRequest>>();
        container.Register<IRequestHandler<AllowedLegacyRequest, int>, RequestHandler<AllowedLegacyRequest>>();
        container.Register<IRequestHandler<AllowedSelfRequest, int>, RequestHandler<AllowedSelfRequest>>();
        container.Register<ICommandHandler<DeniedLegacyCommand>, CommandHandler<DeniedLegacyCommand>>();
        container.Register<ICommandHandler<DeniedSelfCommand>, CommandHandler<DeniedSelfCommand>>();
        container.Register<ICommandHandler<AllowedLegacyCommand>, CommandHandler<AllowedLegacyCommand>>();
        container.Register<ICommandHandler<AllowedSelfCommand>, CommandHandler<AllowedSelfCommand>>();

        container.Verify();
        return container;
    }

    private sealed class Trace
    {
        public int Handled { get; set; }
        public int Commands { get; set; }
    }

    private sealed class DenyPolicy : IAuthorizationPolicy
    {
        public string Name => nameof(DenyPolicy);
        public IReadOnlyList<IAuthorizationRequirement> Requirements { get; } =
            new AuthorizationPolicyBuilder(nameof(DenyPolicy)).RequireAssertion(static _ => false).Build().Requirements;
    }

    private sealed class AllowPolicy : IAuthorizationPolicy
    {
        public string Name => nameof(AllowPolicy);
        public IReadOnlyList<IAuthorizationRequirement> Requirements { get; } =
            new AuthorizationPolicyBuilder(nameof(AllowPolicy)).RequireAssertion(static _ => true).Build().Requirements;
    }

    private interface IValued
    {
        int Value { get; }
    }

#pragma warning disable ARKSOLID001 // Legacy contracts intentionally exercise the v6-style contract shape
    [PolicyAuthorize(typeof(DenyPolicy))]
    private sealed record DeniedLegacyQuery(int Value = 0) : IQuery<int>, IValued;
    [PolicyAuthorize(typeof(AllowPolicy))]
    private sealed record AllowedLegacyQuery(int Value = 1) : IQuery<int>, IValued;
    [PolicyAuthorize(typeof(DenyPolicy))]
    private sealed record DeniedLegacyRequest(int Value = 0) : IRequest<int>, IValued;
    [PolicyAuthorize(typeof(AllowPolicy))]
    private sealed record AllowedLegacyRequest(int Value = 3) : IRequest<int>, IValued;
    [PolicyAuthorize(typeof(DenyPolicy))]
    private sealed record DeniedLegacyCommand(int Value = 0) : ICommand, IValued;
    [PolicyAuthorize(typeof(AllowPolicy))]
    private sealed record AllowedLegacyCommand(int Value = 0) : ICommand, IValued;
#pragma warning restore ARKSOLID001
    [PolicyAuthorize(typeof(DenyPolicy))]
    private sealed record DeniedSelfQuery(int Value = 0) : IQuery<DeniedSelfQuery, int>, IValued;
    [PolicyAuthorize(typeof(AllowPolicy))]
    private sealed record AllowedSelfQuery(int Value = 2) : IQuery<AllowedSelfQuery, int>, IValued;
    [PolicyAuthorize(typeof(DenyPolicy))]
    private sealed record DeniedSelfRequest(int Value = 0) : IRequest<DeniedSelfRequest, int>, IValued;
    [PolicyAuthorize(typeof(AllowPolicy))]
    private sealed record AllowedSelfRequest(int Value = 4) : IRequest<AllowedSelfRequest, int>, IValued;
    [PolicyAuthorize(typeof(DenyPolicy))]
    private sealed record DeniedSelfCommand(int Value = 0) : ICommand<DeniedSelfCommand>, IValued;
    [PolicyAuthorize(typeof(AllowPolicy))]
    private sealed record AllowedSelfCommand(int Value = 0) : ICommand<AllowedSelfCommand>, IValued;

    private sealed class QueryHandler<T>(Trace trace) : IQueryHandler<T, int>
        where T : IQuery<int>, IValued
    {
        public Task<int> ExecuteAsync(T query, CancellationToken ctk = default)
        {
            trace.Handled++;
            return Task.FromResult(query.Value);
        }
    }

    private sealed class RequestHandler<T>(Trace trace) : IRequestHandler<T, int>
        where T : IRequest<int>, IValued
    {
        public Task<int> ExecuteAsync(T Request, CancellationToken ctk = default)
        {
            trace.Handled++;
            return Task.FromResult(Request.Value);
        }
    }

    private sealed class CommandHandler<T>(Trace trace) : ICommandHandler<T>
        where T : ICommand
    {
        public Task ExecuteAsync(T command, CancellationToken ctk = default)
        {
            trace.Handled++;
            trace.Commands++;
            return Task.CompletedTask;
        }
    }
}
