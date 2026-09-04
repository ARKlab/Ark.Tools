// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.MediatorFramework.Sample.Tests.Drivers;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines reusable verbs for asynchronous application workflows.</summary>
[Binding]
public sealed class RebusSteps
{
    private readonly RebusScenarioContext _rebusContext;
    private readonly BookDriver _books;

    /// <summary>Initializes a new instance of the <see cref="RebusSteps"/> class.</summary>
    /// <param name="rebusContext">The scenario-owned Rebus receiver and diagnostics.</param>
    /// <param name="books">The scenario-owned book driver.</param>
    public RebusSteps(RebusScenarioContext rebusContext, BookDriver books)
    {
        _rebusContext = rebusContext;
        _books = books;
    }

    /// <summary>Dispatches a failing Rebus message to exercise retry exhaustion.</summary>
    /// <param name="reason">The expected failure reason.</param>
    [When(@"I dispatch a failing background message with reason ""(.*)""")]
    public async Task DispatchFailingBackgroundMessage(string reason)
    {
        await _rebusContext.SendFailingMessageAsync(reason).ConfigureAwait(false);
    }

    /// <summary>Dispatches a book review through the background bus.</summary>
    /// <param name="table">The review data.</param>
    [When("I dispatch a book review for the current book through the background bus with")]
    public async Task DispatchBookReview(Table table)
    {
        var review = table.CreateInstance<BookReviewBusTable>();
        await _rebusContext.SendBookReviewAsync(_books.Current.Id, review.Rating, review.Text).ConfigureAwait(false);
    }

    /// <summary>Asserts that a failed second-level handler leaves the message in the error queue.</summary>
    [Then("the error queue contains the failed message")]
    public async Task ErrorQueueContainsFailedMessage()
    {
        await _rebusContext.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        _rebusContext.ErrorQueueCount.Should().BeGreaterThan(0);
    }

    /// <summary>Asserts that an unauthorized bus review is failed before its handler runs.</summary>
    [Then("the background bus rejects the book review without invoking its handler")]
    public async Task UnauthorizedBookReviewIsRejected()
    {
        await _rebusContext.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        _rebusContext.ErrorQueueCount.Should().BeGreaterThan(0);
        await _books.ListReviewsAsync().ConfigureAwait(false);
        _books.Reviews.Should().BeEmpty();
    }

}

/// <summary>Defines table data for a review sent through Rebus.</summary>
public sealed record BookReviewBusTable
{
    /// <summary>Gets the review rating.</summary>
    public int Rating { get; init; }

    /// <summary>Gets the review text.</summary>
    public string Text { get; init; } = string.Empty;
}
