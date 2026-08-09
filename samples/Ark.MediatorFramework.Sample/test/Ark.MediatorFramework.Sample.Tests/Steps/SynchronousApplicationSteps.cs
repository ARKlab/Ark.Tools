// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Core;

using AwesomeAssertions;

using FluentValidation;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines direct-contract steps for synchronous application behavior.</summary>
[Binding]
public sealed class SynchronousApplicationSteps
{
    private readonly SampleTestContext _sampleContext;
    private Exception? _exception;
    private EnvelopeBindingResponse? _envelope;
    private ShapeDescription? _shape;
    private bool _refreshCompleted;

    /// <summary>Initializes a new instance of the <see cref="SynchronousApplicationSteps"/> class.</summary>
    /// <param name="sampleContext">The scenario's direct application context.</param>
    public SynchronousApplicationSteps(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Queries an identifier that is not present in the application state.</summary>
    [When("I query a missing greeting")]
    public async Task QueryMissingGreeting()
    {
        _exception = await CaptureAsync(() => Context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
            new GetGreetingQuery { Id = Guid.NewGuid() })).ConfigureAwait(false);
    }

    /// <summary>Dispatches the composed route, query, and body contract.</summary>
    [When("I dispatch an envelope update contract")]
    public async Task DispatchEnvelopeUpdate()
    {
        var id = Guid.NewGuid();
        _envelope = await Context.DispatchRequestAsync<UpdateGreetingRequest, EnvelopeBindingResponse>(
            new UpdateGreetingRequest
            {
                Id = id,
                Audit = "application-test",
                Body = new GreetingUpdateInput { Message = "Composed message" },
            }).ConfigureAwait(false);
    }

    /// <summary>Dispatches an invalid greeting paging query.</summary>
    [When("I search greetings with invalid paging")]
    public async Task SearchGreetingsWithInvalidPaging()
    {
        _exception = await CaptureAsync(() => Context.DispatchQueryAsync<SearchGreetingsQuery, GreetingPage>(
            new SearchGreetingsQuery
            {
                Skip = -1,
                Limit = 0,
            })).ConfigureAwait(false);
    }

    /// <summary>Dispatches a circle through the polymorphic application contract.</summary>
    /// <param name="radius">The circle radius.</param>
    [When(@"I describe a circle with radius (.*)")]
    public async Task DescribeCircle(double radius)
    {
        _shape = await Context.DispatchRequestAsync<DescribeShapeRequest, ShapeDescription>(
            new DescribeShapeRequest
            {
                Shape = new Circle { Radius = radius },
            }).ConfigureAwait(false);
    }

    /// <summary>Dispatches the synchronous refresh command.</summary>
    [When("I dispatch the refresh greeting command")]
    public async Task DispatchRefreshGreeting()
    {
        await Context.DispatchCommandAsync(new RefreshGreetingCommand { Id = Guid.NewGuid() }).ConfigureAwait(false);
        _refreshCompleted = true;
    }

    /// <summary>Asserts the typed missing-entity result.</summary>
    [Then("the request fails with a missing entity exception")]
    public void RequestFailsWithMissingEntity()
    {
        _exception.Should().BeOfType<EntityNotFoundException>();
    }

    /// <summary>Asserts that envelope values reach the handler unchanged.</summary>
    [Then("the envelope response contains the composed values")]
    public void EnvelopeResponseContainsComposedValues()
    {
        _envelope.Should().NotBeNull();
        _envelope!.Id.Should().NotBe(Guid.Empty);
        _envelope.Audit.Should().Be("application-test");
        _envelope.Message.Should().Be("Composed message");
    }

    /// <summary>Asserts both invalid paging fields are reported by validation.</summary>
    [Then("the greeting search fails validation for skip and limit")]
    public void GreetingSearchFailsValidationForSkipAndLimit()
    {
        var exception = _exception.Should().BeOfType<ValidationException>().Which;
        exception.Errors.Should().Contain(error => error.PropertyName == nameof(SearchGreetingsQuery.Skip));
        exception.Errors.Should().Contain(error => error.PropertyName == nameof(SearchGreetingsQuery.Limit));
    }

    /// <summary>Asserts the concrete shape and nested polymorphic result.</summary>
    /// <param name="area">The expected shape area.</param>
    [Then(@"the shape description is a circle with area (.*)")]
    public void ShapeDescriptionIsCircle(double area)
    {
        _shape.Should().NotBeNull();
        _shape!.Shape.Should().BeOfType<Circle>().Which.Radius.Should().Be(2);
        _shape.Metadata.FeaturedShape.Should().BeOfType<Circle>();
        _shape.Area.Should().BeApproximately(area, 0.000000000000001);
    }

    /// <summary>Asserts that the command completed without a transport.</summary>
    [Then("the refresh greeting command completes")]
    public void RefreshGreetingCommandCompletes()
    {
        _refreshCompleted.Should().BeTrue();
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
