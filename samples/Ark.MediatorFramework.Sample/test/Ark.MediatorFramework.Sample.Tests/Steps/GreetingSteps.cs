// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Authorization;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

using AwesomeAssertions;

using FluentValidation;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines application-contract behavioral steps for the greeting sample.</summary>
[Binding]
public sealed class GreetingSteps
{
    private readonly SampleTestContext _sampleContext;
    private GreetingResponse? _greeting;
    private GreetingResponse? _queriedGreeting;
    private GreetingResponseV2? _versionTwoGreeting;
    private PagedResult<AuditRecord>? _audits;
    private Exception? _exception;
    private List<GreetingStreamItem> _streamItems = [];
    private bool _streamWasCancelled;

    /// <summary>Initializes a new instance of the <see cref="GreetingSteps"/> class.</summary>
    /// <param name="context">The scenario's direct application context.</param>
    public GreetingSteps(SampleTestContext context)
    {
        _sampleContext = context;
    }

    /// <summary>Creates a greeting by dispatching its request contract.</summary>
    /// <param name="name">The greeting name.</param>
    [Given(@"I create the greeting ""(.*)""")]
    [When(@"I create the greeting ""(.*)""")]
    public async Task CreateGreeting(string name)
    {
        _greeting = null;
        _exception = await CaptureAsync(async () =>
        {
            _greeting = await Context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
                new CreateGreetingRequest
                {
                    Name = name,
                }).ConfigureAwait(false);
            return _greeting;
        }).ConfigureAwait(false);
    }

    /// <summary>Queries the greeting through its public query contract.</summary>
    [When("I query the greeting")]
    public async Task QueryGreeting()
    {
        _greeting.Should().NotBeNull();
        _exception = await CaptureAsync(async () =>
        {
            _queriedGreeting = await Context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
                new GetGreetingQuery
                {
                    Id = _greeting!.Id,
                }).ConfigureAwait(false);
            return _queriedGreeting;
        }).ConfigureAwait(false);
    }

    /// <summary>Queries the evolved version-two contract.</summary>
    [When("I query the greeting through version two")]
    public async Task QueryGreetingVersionTwo()
    {
        _greeting.Should().NotBeNull();
        _versionTwoGreeting = await Context.DispatchQueryAsync<GetGreetingV2Query, GreetingResponseV2>(
            new GetGreetingV2Query
            {
                Id = _greeting!.Id,
            }).ConfigureAwait(false);
    }

    /// <summary>Consumes a stream with a cancellation token owned by the step.</summary>
    [When("I consume a greeting stream and cancel after two items")]
    public async Task ConsumeGreetingStream()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = await Context.DispatchQueryAsync<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>(
            new GetGreetingsStreamQuery
            {
                Count = 10,
                DelayMilliseconds = 0,
            },
            cancellation.Token).ConfigureAwait(false);

        try
        {
            await foreach (var item in stream.WithCancellation(cancellation.Token).ConfigureAwait(false))
            {
                _streamItems.Add(item);
                if (_streamItems.Count == 2)
                    await cancellation.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _streamWasCancelled = true;
        }
    }

    /// <summary>Reads persisted audit state through the public query contract.</summary>
    [Then(@"the audit query contains a (.*) operation for ""(.*)""")]
    public async Task QueryAudit(string operation, string userId)
    {
        _audits = await Context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                UserId = userId,
                Limit = 25,
            }).ConfigureAwait(false);

        var audit = _audits.Data.Single(record => record.Operation == operation);
        audit.EntityType.Should().Be(nameof(GreetingResponse));
        audit.Identifier.Should().NotBeNullOrWhiteSpace();
        audit.Timestamp.Should().Be(Context.Clock.GetCurrentInstant());
    }

    /// <summary>Asserts that the greeting was returned by its query contract.</summary>
    [Then("the greeting can be queried")]
    public void GreetingCanBeQueried()
    {
        _exception.Should().BeNull();
        _queriedGreeting.Should().NotBeNull();
        _queriedGreeting!.Id.Should().Be(_greeting!.Id);
        _queriedGreeting.Message.Should().Be(_greeting.Message);
    }

    /// <summary>Asserts the typed authorization failure.</summary>
    [Then("the request fails with an authorization exception")]
    public void RequestFailsWithAuthorizationException()
    {
        _exception.Should().BeOfType<PolicyAuthorizationException>();
    }

    /// <summary>Asserts the typed business violation and its domain property.</summary>
    /// <param name="name">The duplicated greeting name.</param>
    [Then(@"the request fails with a greeting already exists violation for ""(.*)""")]
    public void RequestFailsWithBusinessViolation(string name)
    {
        _exception.Should().BeOfType<BusinessRuleViolationException>();
        var violation = ((BusinessRuleViolationException)_exception!).BusinessRuleViolation;
        violation.Should().BeOfType<GreetingAlreadyExistsViolation>();
        ((GreetingAlreadyExistsViolation)violation).Name.Should().Be(name);
    }

    /// <summary>Asserts the typed validation failure.</summary>
    [Then("the request fails validation")]
    public void RequestFailsValidation()
    {
        _exception.Should().BeOfType<ValidationException>();
        ((ValidationException)_exception!).Errors
            .Should().Contain(error => error.PropertyName == nameof(CreateGreetingRequest.Name));
    }

    /// <summary>Asserts the evolved response field.</summary>
    [Then("the version two greeting includes its message length")]
    public void VersionTwoGreetingIncludesMessageLength()
    {
        _versionTwoGreeting.Should().NotBeNull();
        _versionTwoGreeting!.MessageLength.Should().Be(_versionTwoGreeting.Message.Length);
    }

    /// <summary>Asserts stream items and cancellation.</summary>
    [Then("the stream yields two items before cancellation")]
    public void StreamYieldsTwoItemsBeforeCancellation()
    {
        _streamItems.Should().HaveCount(2);
        _streamWasCancelled.Should().BeTrue();
    }

    private static async Task<Exception?> CaptureAsync<T>(Func<Task<T>> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
#pragma warning disable ERP022 // Reqnroll needs the exception for a later typed assertion.
            return exception;
#pragma warning restore ERP022
        }
    }

    private ApplicationTestContext Context => _sampleContext.Application;
}
