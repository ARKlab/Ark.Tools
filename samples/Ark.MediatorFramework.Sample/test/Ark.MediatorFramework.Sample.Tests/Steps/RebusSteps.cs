// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Core;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines reusable verbs for asynchronous application workflows.</summary>
[Binding]
public sealed class RebusSteps
{
    private readonly SampleTestContext _sampleContext;
    private readonly RebusScenarioContext _rebusContext;
    private ComposeGreetingResponse? _composition;
    private GreetingResponse? _greeting;

    /// <summary>Initializes a new instance of the <see cref="RebusSteps"/> class.</summary>
    /// <param name="sampleContext">The scenario-owned direct application sender.</param>
    /// <param name="rebusContext">The scenario-owned Rebus receiver and diagnostics.</param>
    public RebusSteps(SampleTestContext sampleContext, RebusScenarioContext rebusContext)
    {
        _sampleContext = sampleContext;
        _rebusContext = rebusContext;
    }

    /// <summary>Gets the current asynchronous greeting result.</summary>
    public GreetingResponse? Current => _greeting;

    /// <summary>Dispatches a composition request from the supplied table.</summary>
    /// <param name="table">The composition request data.</param>
    [When("I compose a greeting with")]
    public async Task ComposeGreeting(Table table)
    {
        var request = table.CreateInstance<ComposeGreetingRequest>();
        _composition = await Context.DispatchRequestAsync<ComposeGreetingRequest, ComposeGreetingResponse>(request)
            .ConfigureAwait(false);
        _composition.Status.Should().Be("queued");
    }

    /// <summary>Waits for all background and outbox work to complete.</summary>
    [When("I wait for the background bus to be idle and the outbox to be empty")]
    public async Task WaitForBackgroundBus()
    {
        await _rebusContext.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Waits for all non-scheduled background and outbox work to complete.</summary>
    [When("I wait for the background bus to be idle and the outbox to be empty ignoring scheduled messages")]
    public async Task WaitForBackgroundBusIgnoringScheduledMessages()
    {
        await _rebusContext.WaitForIdleAsync(ignoreDeferred: true).ConfigureAwait(false);
    }

    /// <summary>Reads the eventual workflow result through its public query contract.</summary>
    [Then("the background greeting is eventually visible through the query contract")]
    public async Task BackgroundGreetingIsEventuallyVisible()
    {
        _composition.Should().NotBeNull();
        await _rebusContext.WaitForIdleAsync().ConfigureAwait(false);
        _greeting = await Context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
            new GetGreetingQuery { Id = _composition!.Id }).ConfigureAwait(false);
        _greeting.Id.Should().Be(_composition.Id);
    }

    /// <summary>Asserts that Rebus user-context propagation is visible in persisted audit data.</summary>
    /// <param name="userId">The expected authenticated subject.</param>
    [Then(@"the background greeting audit is attributed to ""(.*)""")]
    public async Task BackgroundGreetingAuditIsAttributedTo(string userId)
    {
        _greeting.Should().NotBeNull();
        var audits = await Context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = _greeting!.Id.ToString("D"),
                Limit = 25,
            }).ConfigureAwait(false);

        audits.Data.Should().Contain(record =>
            record.Operation == nameof(CompleteGreetingCompositionRequest)
            && record.UserId == userId);
    }

    /// <summary>Dispatches a failing Rebus message to exercise retry exhaustion.</summary>
    /// <param name="reason">The expected failure reason.</param>
    [When(@"I dispatch a failing background message with reason ""(.*)""")]
    public async Task DispatchFailingBackgroundMessage(string reason)
    {
        await _rebusContext.SendFailingMessageAsync(reason).ConfigureAwait(false);
    }

    /// <summary>Configures the scenario's second-level retry handler to fail.</summary>
    [Given("the failed-message handler fails")]
    public void FailedMessageHandlerFails()
    {
        _rebusContext.FailSecondLevelRetryHandler = true;
    }

    /// <summary>Asserts that the application-owned second-level retry handler receives the original message.</summary>
    [Then("the failed message is handled by the second-level retry handler")]
    public async Task FailedMessageIsHandledBySecondLevelRetryHandler()
    {
        await _rebusContext.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        var failed = await _rebusContext.WaitForFailedMessageAsync().ConfigureAwait(false);
        failed.Message.Should().NotBeNull();
        failed.Exceptions.Should().NotBeEmpty();
    }

    /// <summary>Asserts that a failed second-level handler leaves the message in the error queue.</summary>
    [Then("the error queue contains the failed message")]
    public async Task ErrorQueueContainsFailedMessage()
    {
        await _rebusContext.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        _rebusContext.ErrorQueueCount.Should().BeGreaterThan(0);
    }

    private ApplicationTestContext Context => _sampleContext.Application;
}
