// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.MediatorFramework.AotSample.Core.Application.DAL;
using Ark.MediatorFramework.AotSample.Core.Web.Hosting;
using Ark.MediatorFramework.AotSample.Core.Web.WebInterface;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        using var created = await http.PostAsJsonAsync("/api/v1/books", new Book_CreateRequest.V1 { Title = "Book A" }, ctk).ConfigureAwait(false);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var book = await created.Content.ReadFromJsonAsync<Book>(ctk).ConfigureAwait(false);
        book!.Printed.Should().BeFalse();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        Book? read;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token).ConfigureAwait(false);
            read = await http.GetFromJsonAsync<Book>(new Uri("/api/v1/books/" + book.Id.ToString(CultureInfo.InvariantCulture), UriKind.Relative), timeout.Token).ConfigureAwait(false);
        }
        while (read is not { Printed: true });

        using var missing = await http.GetAsync(new Uri("/api/v1/books/0", UriKind.Relative), ctk).ConfigureAwait(false);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

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
