// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Mime;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>Covers the Minimal API route, authentication, binding, and serialization of the WebRebus host.</summary>
[TestClass]
public sealed class HttpRoundTripTests
{
    /// <summary>Creates a book, reviews it, and reads the review back over HTTP.</summary>
    /// <remarks>
    /// The book create and get contracts are exposed over MCP only; the HTTP surface creates books in bulk and
    /// reads them back through their reviews.
    /// </remarks>
    [TestMethod]
    public async Task CreateThenGetBook()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        using var client = host.CreateClient(
            ApplicationScopes.BookWrite,
            ApplicationScopes.BookReviewsWrite,
            ApplicationScopes.BookReviewsRead);

        using var books = _json("""[{"title":"Dune","author":"Frank Herbert","genre":"Fiction"}]""");
        using var create = await client.PostAsync(
            new Uri("/api/v1/books/bulk", UriKind.Relative),
            books,
            ctk).ConfigureAwait(false);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync(ctk).ConfigureAwait(false));
        var bookId = created.RootElement[0].GetProperty("id").GetGuid();
        bookId.Should().NotBe(Guid.Empty);

        using var body = _json("""{"rating":5,"text":"A desert classic"}""");
        using var review = await client.PostAsync(
            new Uri($"/api/v1/books/{bookId}/reviews", UriKind.Relative),
            body,
            ctk).ConfigureAwait(false);
        review.StatusCode.Should().Be(HttpStatusCode.OK);

        // The generated endpoint binds every query property as required, contract defaults included.
        using var read = await client.GetAsync(
            new Uri($"/api/v1/books/{bookId}/reviews?Skip=0&Limit=25", UriKind.Relative),
            ctk).ConfigureAwait(false);
        read.StatusCode.Should().Be(HttpStatusCode.OK, await read.Content.ReadAsStringAsync(ctk).ConfigureAwait(false));
        using var reviews = JsonDocument.Parse(await read.Content.ReadAsStringAsync(ctk).ConfigureAwait(false));
        reviews.RootElement.EnumerateArray()
            .Select(static item => item.GetProperty("text").GetString())
            .Should().Equal("A desert classic");
    }

    /// <summary>Rejects an anonymous call.</summary>
    [TestMethod]
    public async Task AnonymousCallIsUnauthorized()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        using var client = host.App.GetTestClient();

        using var response = await client.GetAsync(
            new Uri($"/api/v1/books/{Guid.NewGuid()}/reviews?Skip=0&Limit=25", UriKind.Relative),
            host.App.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static StringContent _json(string body)
    {
        return new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json);
    }
}
