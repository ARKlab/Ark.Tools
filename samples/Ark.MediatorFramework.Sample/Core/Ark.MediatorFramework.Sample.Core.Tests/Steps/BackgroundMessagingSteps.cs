// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Tests.Drivers;
using Ark.MediatorFramework.Sample.Core.Tests.Hooks;
using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Core.Tests.Steps;

/// <summary>Defines reusable verbs for asynchronous application workflows.</summary>
[Binding]
public sealed class BackgroundMessagingSteps
{
    private readonly BackgroundMessagingContext _background;
    private readonly SampleTestContext _sampleContext;
    private readonly BookDriver _books;

    /// <summary>Initializes a new instance of the <see cref="BackgroundMessagingSteps"/> class.</summary>
    /// <param name="background">The scenario background messaging observer.</param>
    /// <param name="sampleContext">The scenario-owned application.</param>
    /// <param name="books">The scenario-owned book driver.</param>
    public BackgroundMessagingSteps(
        BackgroundMessagingContext background,
        SampleTestContext sampleContext,
        BookDriver books)
    {
        _background = background;
        _sampleContext = sampleContext;
        _books = books;
    }

    /// <summary>Sends a book review through the api process bus.</summary>
    /// <param name="table">The review data.</param>
    [When("I dispatch a book review for the current book through the background bus with")]
    public async Task DispatchBookReview(Table table)
    {
        var request = table.CreateInstance<CreateBookReviewRequest.V1>() with
        {
            BookId = _books.Current.Id,
            ReviewId = Guid.NewGuid(),
        };
        await _sampleContext.Application.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>Sends one book review twice through the api process bus, as a redelivery would.</summary>
    /// <param name="table">The review data.</param>
    [When("I dispatch the same book review for the current book through the background bus twice with")]
    public async Task DispatchSameBookReviewTwice(Table table)
    {
        var request = table.CreateInstance<CreateBookReviewRequest.V1>() with
        {
            BookId = _books.Current.Id,
            ReviewId = Guid.NewGuid(),
        };
        _books.CurrentReviewId = request.ReviewId;
        await _sampleContext.Application.SendAsync(request).ConfigureAwait(false);
        await _sampleContext.Application.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>Sends a bulk book import through the api process bus.</summary>
    /// <param name="table">The books to import.</param>
    [When("I dispatch a bulk book import through the background bus with")]
    public async Task DispatchBulkBookImport(Table table)
    {
        var request = new Book_BulkCreateRequest.V1(table.CreateSet<Book.V1.Create>().ToArray());
        await _sampleContext.Application.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>Asserts that the failed message was dead-lettered by its second-level handler, not rejected as unknown.</summary>
    [Then("the error queue contains the failed message")]
    public async Task ErrorQueueContainsFailedMessage()
    {
        await ErrorQueueContainsFailedMessage("Background book review failed.").ConfigureAwait(false);
    }

    /// <summary>Asserts that the failed message was dead-lettered by its second-level handler with a description.</summary>
    /// <param name="description">The dead-letter description, the second-level handler's rejection message.</param>
    [Then("the error queue contains the failed message with description '(.*)'")]
    public async Task ErrorQueueContainsFailedMessage(string description)
    {
        await _background.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        _background.ErrorQueueCount.Should().BeGreaterThan(0);
        // A second-level fail-fast dead-letters with the exception type as reason and its message as description.
        _background.DeadLetters.Should().AllSatisfy(deadLetter =>
        {
            deadLetter.Reason.Should().Be(typeof(MessagingFailFastException).FullName);
            deadLetter.Description.Should().Be(description);
        });
    }
}
