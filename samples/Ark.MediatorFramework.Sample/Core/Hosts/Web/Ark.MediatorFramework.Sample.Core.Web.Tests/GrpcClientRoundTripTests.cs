// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.TestHost;

using Client = Ark.MediatorFramework.Sample.Core.Web.GrpcClient;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Calls the Web host through the client generated from the exported <c>.proto</c> files.</summary>
[TestClass]
public sealed class GrpcClientRoundTripTests
{
    /// <summary>Makes a unary and a server-streaming call with the bearer as call metadata.</summary>
    /// <remarks>
    /// The exported Books service exposes only these two methods; <c>StreamBooks</c> yields generated items, not
    /// stored books.
    /// </remarks>
    [TestMethod]
    public async Task DescribeEditionAndStreamBooksThroughGeneratedClient()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        var server = host.App.GetTestServer();
        using var channel = GrpcChannel.ForAddress(
            server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = server.CreateHandler() });
        var client = new Client.BooksV1.BooksV1Client(channel);
        var headers = new Metadata
        {
            { "Authorization", "Bearer " + WebInterfaceTestHost.CreateBearer(ApplicationScopes.BookRead) },
        };

        var edition = await client.DescribeBookEditionAsync(
            new Client.DescribeBookEditionRequest_V1
            {
                Edition = new Client.BookEdition
                {
                    PrintBookEdition = new Client.PrintBookEdition { Format = "Paperback", PageCount = 320 },
                },
            },
            headers,
            cancellationToken: ctk);

        using var call = client.StreamBooks(new Client.StreamBooksQuery_V1 { Count = 2 }, headers, cancellationToken: ctk);
        var titles = new List<string>();
        var authors = new List<string>();
        await foreach (var item in call.ResponseStream.ReadAllAsync(ctk).ConfigureAwait(false))
        {
            titles.Add(item.Title);
            authors.Add(item.Author);
        }

        edition.Description.Should().Be("Paperback print edition with 320 pages");
        titles.Should().Equal("Book 0", "Book 1");
        // The author is a PersonName sensitive value object, exported to .proto as its cleartext string.
        authors.Should().Equal("Author 0", "Author 1");
    }

    /// <summary>
    /// Uploads a cover over HTTP and downloads it through the generated client, whose request carries the
    /// <see cref="BookId"/> value object as the <c>string</c> exported to <c>.proto</c>.
    /// </summary>
    [TestMethod]
    public async Task DownloadBookCoverByValueObjectIdThroughGeneratedClient()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        var bookId = BookId.New();
        byte[] cover = [0x89, 0x50, 0x4E, 0x47];
        using (var http = host.CreateClient(ApplicationScopes.BookCover))
        using (var form = new MultipartFormDataContent())
        using (var file = new ByteArrayContent(cover))
        {
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "attachment", "cover.png");
            using var upload = await http.PostAsync(
                new Uri("/api/v1/books/" + bookId.Value.ToString("D") + "/cover", UriKind.Relative),
                form,
                ctk).ConfigureAwait(false);
            upload.EnsureSuccessStatusCode();
        }

        var server = host.App.GetTestServer();
        using var channel = GrpcChannel.ForAddress(
            server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = server.CreateHandler() });
        var client = new Client.BooksV1.BooksV1Client(channel);
        var headers = new Metadata
        {
            { "Authorization", "Bearer " + WebInterfaceTestHost.CreateBearer(ApplicationScopes.BookCover) },
        };

        using var call = client.DownloadBookCover(
            new Client.DownloadBookCoverQuery_V1 { Id = bookId.Value.ToString("D") },
            headers,
            cancellationToken: ctk);
        var name = string.Empty;
        var data = new List<byte>();
        await foreach (var chunk in call.ResponseStream.ReadAllAsync(ctk).ConfigureAwait(false))
        {
            if (chunk.Metadata is not null)
                name = chunk.Metadata.Name;
            data.AddRange(chunk.Data);
        }

        name.Should().Be("cover.png");
        data.Should().Equal(cover);
    }
}
