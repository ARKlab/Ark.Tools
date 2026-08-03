// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Common.Services.Auth;
using Ark.Tools.Authorization;
using Ark.Tools.Solid;

using BenchmarkDotNet.Attributes;

namespace Ark.Tools.Benchmarks;

public class PolicyAuthorizationMetadataBenchmarks
{
    [Benchmark]
    public object RequestWithNoPolicies()
    {
        return new PolicyAuthorizeOrLogicRequestDecorator<NoPolicyRequest, object>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object RequestWithOnePolicy()
    {
        return new PolicyAuthorizeOrLogicRequestDecorator<OnePolicyRequest, object>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object RequestWithMultiplePolicies()
    {
        return new PolicyAuthorizeOrLogicRequestDecorator<MultiplePolicyRequest, object>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object QueryWithNoPolicies()
    {
        return new PolicyAuthorizeOrLogicQueryDecorator<NoPolicyQuery, object>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object QueryWithOnePolicy()
    {
        return new PolicyAuthorizeOrLogicQueryDecorator<OnePolicyQuery, object>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object QueryWithMultiplePolicies()
    {
        return new PolicyAuthorizeOrLogicQueryDecorator<MultiplePolicyQuery, object>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object CommandWithNoPolicies()
    {
        return new PolicyAuthorizeOrLogicCommandDecorator<NoPolicyCommand>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object CommandWithOnePolicy()
    {
        return new PolicyAuthorizeOrLogicCommandDecorator<OnePolicyCommand>(null!, null!, null!, null!);
    }

    [Benchmark]
    public object CommandWithMultiplePolicies()
    {
        return new PolicyAuthorizeOrLogicCommandDecorator<MultiplePolicyCommand>(null!, null!, null!, null!);
    }

    private sealed class NoPolicyRequest : IRequest<object>
    {
    }

    [PolicyAuthorize("one")]
    private sealed class OnePolicyRequest : IRequest<object>
    {
    }

    [PolicyAuthorize("one")]
    [PolicyAuthorize("two")]
    [PolicyAuthorize("three")]
    private sealed class MultiplePolicyRequest : IRequest<object>
    {
    }

    private sealed class NoPolicyQuery : IQuery<object>
    {
    }

    [PolicyAuthorize("one")]
    private sealed class OnePolicyQuery : IQuery<object>
    {
    }

    [PolicyAuthorize("one")]
    [PolicyAuthorize("two")]
    [PolicyAuthorize("three")]
    private sealed class MultiplePolicyQuery : IQuery<object>
    {
    }

    private sealed class NoPolicyCommand : ICommand
    {
    }

    [PolicyAuthorize("one")]
    private sealed class OnePolicyCommand : ICommand
    {
    }

    [PolicyAuthorize("one")]
    [PolicyAuthorize("two")]
    [PolicyAuthorize("three")]
    private sealed class MultiplePolicyCommand : ICommand
    {
    }
}
