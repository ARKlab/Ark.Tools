// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.MediatorFramework.AotSample.Core.Application;
using Ark.MediatorFramework.AotSample.Core.Application.DAL;
using Ark.MediatorFramework.AotSample.Core.Web.Hosting;
using Ark.MediatorFramework.AotSample.Core.Web.WebInterface;

using Ark.Tools.Compliance;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using System.Net;
using System.Net.Http.Json;

namespace Ark.MediatorFramework.AotSample.Core.Web.Tests;

/// <summary>
/// Runs the WebInterface, the OutboxProcessor and the Processor against SQL Server, Azurite and the Service Bus
/// emulator started by the repository's <c>docker-compose.yml</c>.
/// </summary>
[TestClass]
public sealed class EndToEndTests
{
    private static readonly string[] _args = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreatedBookIsPrintedThroughTheOutbox()
    {
        var ctk = TestContext.CancellationToken;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__Sql")))
            Assert.Inconclusive("Set ConnectionStrings__Sql to the SQL Server of the repository's docker-compose.yml.");

        using var processor = _host(WebHosting.AddProcessor);
        using var outbox = _host(WebHosting.AddOutboxProcessor);
        var builder = WebInterfaceApp.CreateBuilder(_args);
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        await using var __app = app.ConfigureAwait(false);
        await app.Services.GetRequiredService<AotDataContextFactory>().EnsureCreatedAsync(ctk).ConfigureAwait(false);
        WebInterfaceApp.Configure(app);
        await processor.StartAsync(ctk).ConfigureAwait(false);
        await outbox.StartAsync(ctk).ConfigureAwait(false);
        await app.StartAsync(ctk).ConfigureAwait(false);
        using var http = app.GetTestClient();

        var request = new Book_CreateRequest.V1
        {
            Title = "Book A",
            Genre = Genre.Fiction,
            PublishedOn = new LocalDate(2026, 10, 11),
            ReleasedAt = new OffsetDateTime(new LocalDateTime(2026, 10, 11, 9, 0), Offset.FromHours(2)),
            PrintSlot = new LocalTime(8, 30),
            Contact = EmailAddress.From("publisher@example.com"),
        };
        using var content = JsonContent.Create(request, AotJsonContext.Default.Book_CreateRequest_V1);
        using var created = await http.PostAsync(new Uri("/api/v1/books", UriKind.Relative), content, ctk).ConfigureAwait(false);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var book = (await created.Content.ReadFromJsonAsync(AotJsonContext.Default.Book, ctk).ConfigureAwait(false))!;
        book.Printed.Should().BeFalse();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        Book? read;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token).ConfigureAwait(false);
            read = await http.GetFromJsonAsync(
                new Uri("/api/v1/books/" + book.Id.Value.ToString("D", CultureInfo.InvariantCulture), UriKind.Relative), AotJsonContext.Default.Book, timeout.Token).ConfigureAwait(false);
        }
        while (read is not { Printed: true });

        // Every Ark.Tools type survives JSON and SQL Server: the read is the created book plus the print.
        read.Should().BeEquivalentTo(book with { Printed = true, PrintedAt = read.PrintedAt });
        read.PrintedAt.Should().NotBeNull();

        using var missing = await http.GetAsync(new Uri("/api/v1/books/" + Guid.Empty.ToString("D", CultureInfo.InvariantCulture), UriKind.Relative), ctk).ConfigureAwait(false);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var noContact = new StringContent("{\"title\":\"Book B\"}", System.Text.Encoding.UTF8, "application/json");
        using var rejected = await http.PostAsync(new Uri("/api/v1/books", UriKind.Relative), noContact, ctk).ConfigureAwait(false);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await app.StopAsync(ctk).ConfigureAwait(false);
        await outbox.StopAsync(ctk).ConfigureAwait(false);
        await processor.StopAsync(ctk).ConfigureAwait(false);
    }

    private static IHost _host(Action<IServiceCollection, IConfiguration> add)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = _args,
        });
        add(builder.Services, builder.Configuration);
        return builder.Build();
    }
}
