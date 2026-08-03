// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Common.Services.Auth;
using Ark.Tools.Authorization;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleInjector;

using System.Collections.Concurrent;
using System.Security.Claims;

namespace Ark.Reference.Core.Tests.Auth;

[TestClass]
public sealed class PolicyAuthorizeOrLogicDecoratorTests
{
    [TestMethod]
    public async Task NoPolicyPassesThrough()
    {
        var inner = new RequestHandler();
        var decorator = new PolicyAuthorizeOrLogicRequestDecorator<NoPolicyRequest, object>(inner, null!, null!, null!);

        await decorator.ExecuteAsync(new NoPolicyRequest());

        inner.Calls.Should().Be(1);
    }

    [TestMethod]
    public async Task FirstPolicySuccessStopsEvaluation()
    {
        var container = CreateContainer<AuthorizedRequest, TestPolicy>();
        var auth = new AuthorizationService(true);
        var inner = new RequestHandler();
        var decorator = new PolicyAuthorizeOrLogicRequestDecorator<AuthorizedRequest, object>(inner, auth, new UserContext(), container);

        await decorator.ExecuteAsync(new AuthorizedRequest());

        auth.Calls.Should().Be(1);
        inner.Calls.Should().Be(1);
    }

    [TestMethod]
    public async Task AllPoliciesFailurePreservesPolicyMessages()
    {
        var container = CreateContainer<DeniedRequest, TestPolicy>();
        var decorator = new PolicyAuthorizeOrLogicRequestDecorator<DeniedRequest, object>(
            new RequestHandler(), new AuthorizationService(false), new UserContext(), container);

        var action = () => decorator.ExecuteAsync(new DeniedRequest());

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*one*two*");
    }

    [TestMethod]
    public void InheritedAttributesAreCachedOncePerClosedContract()
    {
        var first = new PolicyAuthorizeOrLogicRequestDecorator<InheritedRequest, object>(null!, null!, null!, null!);
        var second = new PolicyAuthorizeOrLogicRequestDecorator<InheritedRequest, object>(null!, null!, null!, null!);
        var field = typeof(PolicyAuthorizeOrLogicRequestDecorator<InheritedRequest, object>)
            .GetField("_policies", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        field!.GetValue(first).Should().BeSameAs(field.GetValue(second));
        ((PolicyAuthorizeAttribute[])field.GetValue(first)!).Length.Should().Be(1);
    }

    [TestMethod]
    public async Task ConcurrentFirstUseSharesMetadata()
    {
        var values = new ConcurrentBag<object?>();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            var decorator = new PolicyAuthorizeOrLogicCommandDecorator<ConcurrentCommand>(null!, null!, null!, null!);
            var field = typeof(PolicyAuthorizeOrLogicCommandDecorator<ConcurrentCommand>)
                .GetField("_policies", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            values.Add(field!.GetValue(decorator));
        })));

        values.Distinct().Count().Should().Be(1);
    }

    private static Container CreateContainer<TRequest, TPolicy>()
        where TRequest : class
        where TPolicy : class, IAuthorizationPolicy
    {
        var container = new Container();
        container.Register<IAuthorizationResourceHandler<TRequest, TPolicy>, ResourceHandler<TRequest, TPolicy>>();
        return container;
    }

    private sealed class RequestHandler : IRequestHandler<NoPolicyRequest, object>,
        IRequestHandler<AuthorizedRequest, object>, IRequestHandler<DeniedRequest, object>
    {
        public int Calls { get; private set; }

        public Task<object> ExecuteAsync(NoPolicyRequest request, CancellationToken ctk = default)
        {
            Calls++;
            return Task.FromResult<object>(request);
        }

        public Task<object> ExecuteAsync(AuthorizedRequest request, CancellationToken ctk = default)
        {
            Calls++;
            return Task.FromResult<object>(request);
        }

        public Task<object> ExecuteAsync(DeniedRequest request, CancellationToken ctk = default)
        {
            Calls++;
            return Task.FromResult<object>(request);
        }
    }

    private sealed class AuthorizationService(bool authorized) : IAuthorizationService
    {
        public int Calls { get; private set; }
        public IAuthorizationPolicyProvider PolicyProvider { get; } = new PolicyProvider();

        public Task<(bool, IList<string>)> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName, CancellationToken ctk = default)
        {
            return Task.FromResult((authorized, (IList<string>)new[] { policyName }));
        }

        public Task<(bool, IList<string>)> AuthorizeAsync(ClaimsPrincipal user, object? resource, IAuthorizationPolicy policy, CancellationToken ctk = default)
        {
            Calls++;
            return Task.FromResult((authorized, (IList<string>)new[] { policy.Name }));
        }
    }

    private sealed class PolicyProvider : IAuthorizationPolicyProvider
    {
        public Task<IAuthorizationPolicy?> GetPolicyAsync(string policyName, CancellationToken ctk = default)
        {
            return Task.FromResult<IAuthorizationPolicy?>(new TestPolicy(policyName));
        }
    }

    private sealed class UserContext : IContextProvider<ClaimsPrincipal>
    {
        public ClaimsPrincipal Current { get; } = new(new ClaimsIdentity());
    }

    private sealed class ResourceHandler<TRequest, TPolicy> : IAuthorizationResourceHandler<TRequest, TPolicy>
        where TRequest : class
        where TPolicy : class, IAuthorizationPolicy
    {
        public Task<object> GetResouceAsync(TRequest query, CancellationToken ctk = default)
        {
            return Task.FromResult<object>(query);
        }
    }

    private sealed class TestPolicy : IAuthorizationPolicy
    {
        public TestPolicy()
        {
            Name = "one";
        }

        public TestPolicy(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public IReadOnlyList<IAuthorizationRequirement> Requirements { get; } = Array.Empty<IAuthorizationRequirement>();
    }

    private sealed class NoPolicyRequest : IRequest<object>
    {
    }

    [PolicyAuthorize(typeof(TestPolicy))]
    private sealed class AuthorizedRequest : IRequest<object>
    {
    }

    [PolicyAuthorize("one")]
    [PolicyAuthorize("two")]
    private sealed class DeniedRequest : IRequest<object>
    {
    }

    [PolicyAuthorize("inherited")]
    private class BaseRequest
    {
    }

    private sealed class InheritedRequest : BaseRequest, IRequest<object>
    {
    }

    [PolicyAuthorize("concurrent")]
    private sealed class ConcurrentCommand : ICommand
    {
    }
}
