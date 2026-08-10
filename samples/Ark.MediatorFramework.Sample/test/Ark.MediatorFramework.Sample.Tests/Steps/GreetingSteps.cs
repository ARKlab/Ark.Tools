// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Authorization;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Core.EntityTag;

using AwesomeAssertions;

using FluentValidation;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines application-contract behavioral steps for the greeting sample.</summary>
[Binding]
public sealed class GreetingSteps
{
    private readonly SampleTestContext _sampleContext;
    private readonly RebusScenarioContext _rebusContext;
    private Greeting.V1.Output? _greeting;
    private GreetingResponse? _queriedGreeting;
    private ComposeGreetingResponse? _composition;
    private GreetingResponseV2? _versionTwoGreeting;
    private PagedResult<AuditRecord>? _audits;
    private GreetingPage? _greetingPage;
    private string? _previousETag;
    private Exception? _exception;
    private List<GreetingStreamItem> _streamItems = [];
    private bool _streamWasCancelled;

    /// <summary>Initializes a new instance of the <see cref="GreetingSteps"/> class.</summary>
    /// <param name="context">The scenario's direct application context.</param>
    /// <param name="rebusContext">The scenario's background bus context.</param>
    public GreetingSteps(SampleTestContext context, RebusScenarioContext rebusContext)
    {
        _sampleContext = context;
        _rebusContext = rebusContext;
    }

    /// <summary>Gets the active greeting in the current scenario.</summary>
    public Greeting.V1.Output? Current => _greeting;

    /// <summary>Creates and activates a greeting from a table-defined request.</summary>
    /// <param name="table">The greeting request data.</param>
    [Given("I create a greeting with")]
    public async Task GivenCreateGreeting(Table table)
    {
        await CreateGreeting(table).ConfigureAwait(false);
        _exception.Should().BeNull();
        _greeting.Should().NotBeNull();
    }

    [When("I create a greeting with")]
    public async Task CreateGreeting(Table table)
    {
        var request = table.CreateInstance<Greeting.V1.Create>();
        await CreateGreetingAsync(request).ConfigureAwait(false);
    }

    /// <summary>Creates greetings from table rows and activates the last result.</summary>
    /// <param name="table">The greeting request data.</param>
    [Given("I create greetings with")]
    public async Task GivenCreateGreetings(Table table)
    {
        await CreateGreetings(table).ConfigureAwait(false);
        _exception.Should().BeNull();
        _greeting.Should().NotBeNull();
    }

    [When("I create greetings with")]
    public async Task CreateGreetings(Table table)
    {
        foreach (var request in table.CreateSet<Greeting.V1.Create>())
            await CreateGreetingAsync(request).ConfigureAwait(false);
    }

    /// <summary>Loads the active greeting through its public query contract.</summary>
    [When("I retrieve the current greeting")]
    public async Task RetrieveCurrentGreeting()
    {
        _greeting.Should().NotBeNull();
        _exception = await CaptureAsync(async () =>
        {
            var queried = await Context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
                new GetGreetingQuery { Id = _greeting!.Id }).ConfigureAwait(false);
            _queriedGreeting = queried;
            _greeting = ToOutput(queried);
            return _queriedGreeting;
        }).ConfigureAwait(false);
    }

    /// <summary>Updates the active greeting from a table-defined request.</summary>
    /// <param name="table">The replacement greeting data.</param>
    [When("I update the current greeting with")]
    public async Task UpdateCurrentGreeting(Table table)
    {
        _greeting.Should().NotBeNull();
        _previousETag = _greeting!.ETag;
        var request = new Greeting_UpdateRequest.V1(
            table.CreateInstance<Greeting.V1.Input>(),
            _greeting.Id,
            _greeting.ETag);
        _greeting = await Context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(request)
            .ConfigureAwait(false);
    }

    /// <summary>Attempts an update with the ETag from before the latest successful update.</summary>
    /// <param name="table">The replacement greeting data.</param>
    [When("I update the current greeting with a stale ETag and")]
    public async Task UpdateCurrentGreetingWithStaleETag(Table table)
    {
        _greeting.Should().NotBeNull();
        _previousETag.Should().NotBeNullOrWhiteSpace();
        var request = new Greeting_UpdateRequest.V1(
            table.CreateInstance<Greeting.V1.Input>(),
            _greeting!.Id,
            _previousETag);
        _exception = await CaptureAsync(() =>
            Context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(request)).ConfigureAwait(false);
    }

    /// <summary>Searches greetings using a table-defined query.</summary>
    /// <param name="table">The search query data.</param>
    [When("I search greetings by")]
    public async Task SearchGreetings(Table table)
    {
        var query = table.CreateInstance<SearchGreetingsQuery>();
        _greetingPage = await Context.DispatchQueryAsync<SearchGreetingsQuery, GreetingPage>(query)
            .ConfigureAwait(false);
    }

    /// <summary>Asserts that the active greeting matches the supplied table.</summary>
    /// <param name="table">The expected greeting data.</param>
    [Then("the current greeting is")]
    public void CurrentGreetingIs(Table table)
    {
        _greeting.Should().NotBeNull();
        table.CompareToInstance(_greeting!);
    }

    /// <summary>Asserts that the current greeting has a changed, opaque concurrency token.</summary>
    [Then("the current greeting has a refreshed opaque ETag")]
    public void CurrentGreetingHasRefreshedOpaqueETag()
    {
        _greeting.Should().NotBeNull();
        _greeting!.ETag.Should().NotBeNullOrWhiteSpace();
        _greeting.ETag.Should().NotBe(_previousETag);
    }

    /// <summary>Asserts that an application mutation wrote a deterministic audit record.</summary>
    /// <param name="operation">The expected operation name.</param>
    [Then(@"the current greeting has a deterministic audit for ""(.*)""")]
    public async Task CurrentGreetingHasDeterministicAudit(string operation)
    {
        _greeting.Should().NotBeNull();
        var audits = await Context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = _greeting!.Id.ToString("D"),
                Limit = 25,
            }).ConfigureAwait(false);
        var audit = audits.Data.Single(record => record.Operation == operation);
        audit.UserId.Should().Be("test-user");
        audit.EntityType.Should().Be(nameof(GreetingResponse));
        audit.Timestamp.Should().Be(Context.Clock.GetCurrentInstant());
    }

    /// <summary>Asserts the typed stale-ETag failure.</summary>
    [Then("the request fails because the greeting ETag is stale")]
    public void RequestFailsBecauseGreetingETagIsStale()
    {
        _exception.Should().BeOfType<EntityTagMismatchException>();
    }

    /// <summary>Asserts that the active greeting audit matches the supplied table.</summary>
    /// <param name="table">The expected audit data.</param>
    [Then("the current greeting audit is")]
    public async Task CurrentGreetingAuditIs(Table table)
    {
        _greeting.Should().NotBeNull();
        var audits = await Context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = _greeting!.Id.ToString("D"),
                Limit = 25,
            }).ConfigureAwait(false);
        var audit = audits.Data.Single();
        table.CompareToInstance(audit);
    }

    /// <summary>Asserts the current greeting-search result count.</summary>
    /// <param name="count">The expected count.</param>
    [Then(@"the greeting search has (.*) results")]
    public void GreetingSearchHasResults(long count)
    {
        _greetingPage.Should().NotBeNull();
        _greetingPage!.Count.Should().Be(count);
    }

    /// <summary>Asserts the current greeting-search result set.</summary>
    /// <param name="table">The expected greeting data.</param>
    [Then("the greeting search contains")]
    public void GreetingSearchContains(Table table)
    {
        _greetingPage.Should().NotBeNull();
        table.CompareToSet(_greetingPage!.Data);
    }

    /// <summary>Creates a greeting by dispatching its request contract.</summary>
    /// <param name="name">The greeting name.</param>
    [Given(@"I create the greeting ""(.*)""")]
    public async Task GivenCreateGreeting(string name)
    {
        await CreateGreeting(name).ConfigureAwait(false);
        _exception.Should().BeNull();
        _greeting.Should().NotBeNull();
    }

    [When(@"I create the greeting ""(.*)""")]
    public async Task CreateGreeting(string name)
    {
        _greeting = null;
        _exception = await CaptureAsync(async () =>
        {
            _greeting = await Context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
                new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = name })).ConfigureAwait(false);
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
            var queried = await Context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
                new GetGreetingQuery
                {
                    Id = _greeting!.Id,
                }).ConfigureAwait(false);
            _queriedGreeting = queried;
            _greeting = ToOutput(queried);
            return queried;
        }).ConfigureAwait(false);
    }

    /// <summary>Queues a greeting composition through the application contract.</summary>
    /// <param name="table">The composition request data.</param>
    [When("I compose a greeting with")]
    public async Task ComposeGreeting(Table table)
    {
        _composition = await Context.DispatchRequestAsync<ComposeGreetingRequest, ComposeGreetingResponse>(
            table.CreateInstance<ComposeGreetingRequest>()).ConfigureAwait(false);
        _composition.Status.Should().Be("queued");
    }

    /// <summary>Observes the queued greeting through its public query contract.</summary>
    [Then("the background greeting is eventually visible through the query contract")]
    public async Task BackgroundGreetingIsEventuallyVisible()
    {
        _composition.Should().NotBeNull();
        await _rebusContext.WaitForIdleAsync().ConfigureAwait(false);
        _queriedGreeting = await Context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
            new GetGreetingQuery { Id = _composition!.Id }).ConfigureAwait(false);
        _queriedGreeting.Id.Should().Be(_composition.Id);
    }

    /// <summary>Asserts that the queued greeting keeps the authenticated user in its audit.</summary>
    /// <param name="userId">The expected user identifier.</param>
    [Then(@"the background greeting audit is attributed to ""(.*)""")]
    public async Task BackgroundGreetingAuditIsAttributedTo(string userId)
    {
        _queriedGreeting.Should().NotBeNull();
        var audits = await Context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = _queriedGreeting!.Id.ToString("D"),
                Limit = 25,
            }).ConfigureAwait(false);
        audits.Data.Should().Contain(record =>
            record.Operation == nameof(CompleteGreetingCompositionRequest)
            && record.UserId == userId);
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
        audit.UserId.Should().Be(userId);
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
            .Should().Contain(error => error.PropertyName == "Data.Name");
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

    private async Task CreateGreetingAsync(Greeting.V1.Create request)
    {
        _greeting = null;
        _exception = await CaptureAsync(async () =>
        {
            _greeting = await Context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
                    new Greeting_CreateRequest.V1(request))
                .ConfigureAwait(false);
            return _greeting;
        }).ConfigureAwait(false);
    }

    private ApplicationTestContext Context => _sampleContext.Application;

    private static Greeting.V1.Output ToOutput(GreetingResponse greeting)
    {
        return new Greeting.V1.Output
        {
            Id = greeting.Id,
            Message = greeting.Message,
            Date = greeting.Date,
            DateTime = greeting.DateTime,
            OffsetDateTime = greeting.OffsetDateTime,
            Period = greeting.Period,
            AuditId = greeting.AuditId,
            ETag = greeting.ETag,
        };
    }
}
