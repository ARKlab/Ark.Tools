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
        };
        await _sampleContext.Application.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>Asserts that the failed message was dead-lettered by its second-level handler, not rejected as unknown.</summary>
    [Then("the error queue contains the failed message")]
    public async Task ErrorQueueContainsFailedMessage()
    {
        await _background.WaitForIdleAsync(allowErrors: true).ConfigureAwait(false);
        _background.ErrorQueueCount.Should().BeGreaterThan(0);
        // A second-level fail-fast dead-letters with the exception type as reason and its message as description.
        _background.DeadLetters.Should().AllSatisfy(static deadLetter =>
        {
            deadLetter.Reason.Should().Be(typeof(MessagingFailFastException).FullName);
            deadLetter.Description.Should().Be("Background book review failed.");
        });
    }
}
