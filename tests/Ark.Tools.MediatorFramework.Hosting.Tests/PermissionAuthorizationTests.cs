// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Claims;

using Ark.Tools.Authorization;
using Ark.Tools.Authorization.Requirement;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Verifies permission authorization requirement matching and caching behavior.</summary>
[TestClass]
public sealed class PermissionAuthorizationTests
{
    [TestMethod]
    public async Task NullResourceMatchesOnlyNonResourceRequirement()
    {
        var provider = new TestPermissionsProvider(TestPermission.Read);
        var handler = new PermissionAuthorizationHandler<TestPermission>(provider);
        var nonResourceRequirement = new PermissionAuthorizationRequirement<TestPermission>(TestPermission.Read);
        var resourceRequirement = new PermissionAuthorizationRequirement<TestPermission, ResourceA>(TestPermission.Read);
        var policy = new AuthorizationPolicyBuilder(nameof(NullResourceMatchesOnlyNonResourceRequirement))
            .AddRequirements(nonResourceRequirement, resourceRequirement)
            .Build();
        var context = CreateContext(policy, resource: null);

        await handler.HandleAsync(context).ConfigureAwait(false);

        context.SucceededRequirements.Should().Contain(nonResourceRequirement);
        context.PendingRequirements.Should().Contain(resourceRequirement);
        provider.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task ResourceTypeMatchesOnlyItsRequirement()
    {
        var provider = new TestPermissionsProvider(TestPermission.Read);
        var handler = new PermissionAuthorizationHandler<TestPermission>(provider);
        var resourceARequirement = new PermissionAuthorizationRequirement<TestPermission, ResourceA>(TestPermission.Read);
        var resourceBRequirement = new PermissionAuthorizationRequirement<TestPermission, ResourceB>(TestPermission.Read);
        var unrelatedRequirement = new DenyAnonymousAuthorizationRequirement();
        var policy = new AuthorizationPolicyBuilder(nameof(ResourceTypeMatchesOnlyItsRequirement))
            .AddRequirements(resourceARequirement, resourceBRequirement, unrelatedRequirement)
            .Build();
        var context = CreateContext(policy, new ResourceA());

        await handler.HandleAsync(context).ConfigureAwait(false);

        context.SucceededRequirements.Should().Contain(resourceARequirement);
        context.PendingRequirements.Should().Contain(resourceBRequirement);
        context.PendingRequirements.Should().Contain(unrelatedRequirement);
        provider.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task MissingPermissionLeavesRequirementPending()
    {
        var provider = new TestPermissionsProvider();
        var handler = new PermissionAuthorizationHandler<TestPermission>(provider);
        var requirement = new PermissionAuthorizationRequirement<TestPermission, ResourceA>(TestPermission.Read);
        var context = CreateContext(
            new AuthorizationPolicyBuilder(nameof(MissingPermissionLeavesRequirementPending))
                .AddRequirements(requirement)
                .Build(),
            new ResourceA());

        await handler.HandleAsync(context).ConfigureAwait(false);

        context.PendingRequirements.Should().Contain(requirement);
        context.SucceededRequirements.Should().NotContain(requirement);
        context.HasSucceeded.Should().BeFalse();
        provider.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task UnrelatedPolicyDoesNotInvokeProvider()
    {
        var provider = new TestPermissionsProvider(TestPermission.Read);
        var handler = new PermissionAuthorizationHandler<TestPermission>(provider);
        var requirement = new DenyAnonymousAuthorizationRequirement();
        var context = CreateContext(
            new AuthorizationPolicyBuilder(nameof(UnrelatedPolicyDoesNotInvokeProvider))
                .AddRequirements(requirement)
                .Build(),
            new ResourceA());

        await handler.HandleAsync(context).ConfigureAwait(false);

        context.PendingRequirements.Should().Contain(requirement);
        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task ConcurrentFirstUseSupportsMultipleResourceTypes()
    {
        var provider = new TestPermissionsProvider(TestPermission.Read);
        var handler = new PermissionAuthorizationHandler<TestPermission>(provider);
        var contexts = Enumerable.Range(0, 32)
            .Select(index => CreateContext(
                new AuthorizationPolicyBuilder($"Concurrent-{index}")
                    .AddRequirements(index % 2 == 0
                        ? new PermissionAuthorizationRequirement<TestPermission, ResourceA>(TestPermission.Read)
                        : new PermissionAuthorizationRequirement<TestPermission, ResourceB>(TestPermission.Read))
                    .Build(),
                index % 2 == 0 ? new ResourceA() : new ResourceB()))
            .ToArray();

        await Task.WhenAll(contexts.Select(context => handler.HandleAsync(context))).ConfigureAwait(false);

        contexts.All(context => context.HasSucceeded).Should().BeTrue();
        provider.CallCount.Should().Be(contexts.Length);
    }

    private static AuthorizationContext CreateContext(IAuthorizationPolicy policy, object? resource)
    {
        return new AuthorizationContext(policy, new ClaimsPrincipal(new ClaimsIdentity()), resource);
    }

    private enum TestPermission
    {
        Read,
    }

    private sealed class ResourceA
    {
    }

    private sealed class ResourceB
    {
    }

    private sealed class TestPermissionsProvider : IUserPermissionsProvider<TestPermission>
    {
        private readonly IReadOnlyCollection<TestPermission> _permissions;
        private int _callCount;

        public TestPermissionsProvider(params TestPermission[] permissions)
        {
            _permissions = permissions;
        }

        public int CallCount => _callCount;

        public Task<IEnumerable<TestPermission>> GetPermissions(AuthorizationContext context)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult<IEnumerable<TestPermission>>(_permissions);
        }
    }
}
