// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Claims;

using Ark.Tools.Authorization;
using Ark.Tools.Authorization.Requirement;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Ark.Tools.Benchmarks;

/// <summary>Compares cached and uncached permission authorization checks across resource types.</summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class PermissionAuthorizationBenchmarks
{
    private readonly BenchmarkPermissionsProvider _provider = new();
    private readonly PermissionAuthorizationHandler<Permission> _handler;

    /// <summary>Initializes the benchmark handler.</summary>
    public PermissionAuthorizationBenchmarks()
    {
        _handler = new PermissionAuthorizationHandler<Permission>(_provider);
    }

    /// <summary>Measures repeated authorization checks that construct the closed requirement type.</summary>
    [Benchmark(Baseline = true)]
    public async Task UncachedAuthorizationChecks()
    {
        foreach (var context in _createContexts())
        {
            await _handleUncachedAsync(context).ConfigureAwait(false);
        }
    }

    /// <summary>Measures repeated authorization checks using the closed requirement type cache.</summary>
    [Benchmark]
    public async Task CachedAuthorizationChecks()
    {
        foreach (var context in _createContexts())
        {
            await _handler.HandleAsync(context).ConfigureAwait(false);
        }
    }

    private async Task _handleUncachedAsync(AuthorizationContext context)
    {
        var permissionType = typeof(PermissionAuthorizationRequirement<,>).MakeGenericType(
            typeof(Permission),
            context.Resource!.GetType());
        var requirements = context.Policy.Requirements
            .Where(requirement => permissionType.IsAssignableFrom(requirement.GetType()))
            .Cast<PermissionAuthorizationRequirement<Permission>>()
            .ToArray();
        if (requirements.Length == 0)
        {
            return;
        }

        var permissions = await _provider.GetPermissions(context).ConfigureAwait(false);
        if (permissions == null || !permissions.Any())
        {
            return;
        }

        foreach (var requirement in requirements)
        {
            if (permissions.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }

    private static AuthorizationContext[] _createContexts()
    {
        return
        [
            _createContext(new ResourceA(), "resource-a-1"),
            _createContext(new ResourceB(), "resource-b-1"),
            _createContext(new ResourceA(), "resource-a-2"),
            _createContext(new ResourceB(), "resource-b-2"),
        ];
    }

    private static AuthorizationContext _createContext(object resource, string name)
    {
        var builder = new AuthorizationPolicyBuilder(name);
        if (resource is ResourceA)
        {
            builder.RequireUserPermission<ResourceA, Permission>(Permission.Read);
        }
        else
        {
            builder.RequireUserPermission<ResourceB, Permission>(Permission.Read);
        }

        return new AuthorizationContext(
            builder.Build(),
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);
    }

    private enum Permission
    {
        Read,
    }

    private sealed class ResourceA
    {
    }

    private sealed class ResourceB
    {
    }

    private sealed class BenchmarkPermissionsProvider : IUserPermissionsProvider<Permission>
    {
        public Task<IEnumerable<Permission>> GetPermissions(AuthorizationContext context)
        {
            return Task.FromResult<IEnumerable<Permission>>([Permission.Read]);
        }
    }

    /// <summary>Configures a short in-process .NET 10 benchmark run.</summary>
    public sealed class BenchmarkConfig : ManualConfig
    {
        /// <summary>Initializes the benchmark configuration.</summary>
        public BenchmarkConfig()
        {
            AddJob(Job.InProcess
                .WithLaunchCount(1)
                .WithWarmupCount(5)
                .WithIterationCount(15));
            AddDiagnoser(BenchmarkDotNet.Diagnosers.MemoryDiagnoser.Default);
        }
    }
}
