// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines reusable verbs for asynchronous application workflows.</summary>
[Binding]
public sealed class RebusSteps
{
    private readonly RebusScenarioContext _rebusContext;

    /// <summary>Initializes a new instance of the <see cref="RebusSteps"/> class.</summary>
    /// <param name="rebusContext">The scenario-owned Rebus receiver and diagnostics.</param>
    public RebusSteps(RebusScenarioContext rebusContext)
    {
        _rebusContext = rebusContext;
    }

    /// <summary>Dispatches a failing Rebus message to exercise retry exhaustion.</summary>
    /// <param name="reason">The expected failure reason.</param>
    [When(@"I dispatch a failing background message with reason ""(.*)""")]
    public async Task DispatchFailingBackgroundMessage(string reason)
    {
        await _rebusContext.SendFailingMessageAsync(reason).ConfigureAwait(false);
    }

    /// <summary>Asserts that a failed second-level handler leaves the message in the error queue.</summary>
    [Then("the error queue contains the failed message")]
    public async Task ErrorQueueContainsFailedMessage()
    {
        await _rebusContext.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        _rebusContext.ErrorQueueCount.Should().BeGreaterThan(0);
    }

}
