// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using MessagePack;
using MessagePack.Resolvers;

using System.Net;
using System.Net.Mime;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Covers the Minimal API route, authentication, binding, and serialization of the Web host.</summary>
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

    /// <summary>Describes a Book edition over HTTP, requesting and answering in MessagePack.</summary>
    [TestMethod]
    public async Task DescribeBookEditionRoundTripsOverMessagePack()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        using var client = host.CreateClient(ApplicationScopes.BookRead);
        var options = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(
                MessagePack.NodaTime.NodatimeResolver.Instance,
                DynamicEnumAsStringResolver.Instance,
                StandardResolver.Instance));

        using var body = new ByteArrayContent(MessagePackSerializer.Serialize(
            new DescribeBookEditionRequest.V1 { Edition = new PrintBookEdition { Format = "Paperback", PageCount = 320 } },
            options,
            ctk));
        body.Headers.ContentType = new("application/x-msgpack");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/books/editions/describe", UriKind.Relative))
        {
            Content = body,
        };
        request.Headers.Accept.ParseAdd("application/x-msgpack");

        using var response = await client.SendAsync(request, ctk).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/x-msgpack");
        var description = MessagePackSerializer.Deserialize<BookEditionDescription>(
            await response.Content.ReadAsByteArrayAsync(ctk).ConfigureAwait(false),
            options,
            ctk);
        description.Description.Should().Be("Paperback print edition with 320 pages");
    }

    /// <summary>Streams Book items over HTTP and reads them back in order.</summary>
    [TestMethod]
    public async Task StreamBooksReturnsItemsInOrder()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        using var client = host.CreateClient(ApplicationScopes.BookRead);

        // The generated endpoint accepts MessagePack, so it reads a request body even for GET: send an empty JSON
        // object and let the query string supply the values.
        using var empty = _json("{}");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/v1/books/stream?Count=3&DelayMilliseconds=0", UriKind.Relative))
        {
            Content = empty,
        };

        using var response = await client.SendAsync(request, ctk).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ctk).ConfigureAwait(false));
        using var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ctk).ConfigureAwait(false));
        items.RootElement.EnumerateArray()
            .Select(static item => item.GetProperty("index").GetInt32())
            .Should().Equal(0, 1, 2);
    }

    private static StringContent _json(string body)
    {
        return new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json);
    }
}
