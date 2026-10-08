// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using AwesomeAssertions;

using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>Runs the four WebRebus processes on one in-memory Rebus network.</summary>
[TestClass]
public sealed class RebusTopologyTests
{
    /// <summary>A print completed by the worker reaches the notification and audit subscribers.</summary>
    [TestMethod]
    public async Task CompletedPrintReachesBothRebusSubscribers()
    {
        var network = new InMemNetwork();
        var subscribers = new InMemorySubscriberStore();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var notifications = new RecordingSink();
        var audits = new RecordingSink();
        await using var api = await WebRebusTestHosts.ApiAsync(network, subscribers, factory).ConfigureAwait(false);
        await using var worker = await WebRebusTestHosts.WorkerAsync(network, subscribers, factory).ConfigureAwait(false);
        await using var notification = await WebRebusTestHosts.NotificationAsync(network, subscribers, factory, notifications).ConfigureAwait(false);
        await using var audit = await WebRebusTestHosts.AuditAsync(network, subscribers, factory, audits).ConfigureAwait(false);

        var book = await api.DispatchAsync<Book_CreateRequest.V1, Book.V1.Output>(WebRebusTestHosts.NewBook())
            .ConfigureAwait(false);
        await api.DispatchAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(
            new CreateBookPrintProcessRequest.V1 { BookId = book.Id }).ConfigureAwait(false);

        await WebRebusTestHosts.WaitUntilAsync(() => notifications.Count == 1 && audits.Count == 1)
            .ConfigureAwait(false);
        notifications.BookIds.Should().ContainSingle().Which.Should().Be(book.Id);
    }

    /// <summary>A background review sent without the review scope exhausts its retries and is dead-lettered.</summary>
    [TestMethod]
    public async Task UnauthorizedBackgroundReviewMovesToErrorQueue()
    {
        var network = new InMemNetwork();
        var subscribers = new InMemorySubscriberStore();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        await using var api = await WebRebusTestHosts.ApiAsync(network, subscribers, factory).ConfigureAwait(false);
        await using var worker = await WebRebusTestHosts.WorkerAsync(network, subscribers, factory).ConfigureAwait(false);
        var book = await api.DispatchAsync<Book_CreateRequest.V1, Book.V1.Output>(WebRebusTestHosts.NewBook())
            .ConfigureAwait(false);

        api.SetScopes(ApplicationScopes.BookRead, ApplicationScopes.BookWrite);
        await api.SendAsync(new CreateBookReviewRequest.V1 { ReviewId = Guid.NewGuid(), BookId = book.Id, Rating = 5, Text = "Good" })
            .ConfigureAwait(false);

        await WebRebusTestHosts.WaitUntilAsync(() => network.GetCount("error") > 0).ConfigureAwait(false);
    }
}
