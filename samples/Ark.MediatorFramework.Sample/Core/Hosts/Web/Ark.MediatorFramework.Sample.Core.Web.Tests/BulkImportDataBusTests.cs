// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using SimpleInjector;

using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Sends an oversized bulk import from the api producer to the worker, as their <c>Program.cs</c> compose them.</summary>
[TestClass]
public sealed class BulkImportDataBusTests
{
    private const int _bulkImportSize = 2_000;

    /// <summary>
    /// A bulk import bigger than the transport inline limit travels as a DataBus claim check and the worker creates
    /// every book.
    /// </summary>
    [TestMethod]
    public async Task OversizedBulkImportTravelsThroughTheDataBus()
    {
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var transport = new InMemoryMessagingTransport();
        // The attachment lifetime must outlive the network's one-hour maximum scheduling delay.
        var dataBus = new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2));
        await using var api = await _startAsync<SampleMessagingApiParticipant>(factory, transport, dataBus, receiver: false)
            .ConfigureAwait(false);
        await using var worker = await _startAsync<SampleMessagingParticipant>(factory, transport, dataBus, receiver: true)
            .ConfigureAwait(false);
        var author = "Bulk " + Guid.NewGuid().ToString("N");

        await api.Container.GetInstance<IBus>().Send(_newOversizedBulkImport(author)).ConfigureAwait(false);

        await _waitUntilAsync(
                async () => await _countBooksAsync(factory, author).ConfigureAwait(false) == _bulkImportSize,
                () => _describeQueue(transport, SampleMessagingParticipant.Identity))
            .ConfigureAwait(false);
        dataBus.Count.Should().Be(1, "the body travels as one claim check, not inline");
    }

    private static async Task<Process> _startAsync<TParticipant>(
        InMemorySampleDataContextFactory factory,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        bool receiver)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
#pragma warning disable CA2000 // The returned process owns and disposes the container and the provider.
        var container = WebHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        if (!receiver)
            container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new AllScopesUser());
        var services = new ServiceCollection();
        services.AddLogging();
        WebHosting.AddParticipant<TParticipant>(services, container, transport, dataBus, resourceManagement: null, receiver);
        var provider = services.BuildServiceProvider();
#pragma warning restore CA2000
        WebHosting.BridgeBus(container, () => provider);
        container.Verify();
        var hosted = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hosted)
            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return new Process(container, provider, hosted);
    }

    private static Book_BulkCreateRequest.V1 _newOversizedBulkImport(string author)
    {
        // About 2,000 x 200 bytes of random Base64 titles, which compress poorly: the payload stays above the
        // offload threshold even if messaging compression is enabled.
        return new Book_BulkCreateRequest.V1(Enumerable.Range(0, _bulkImportSize)
            .Select(_ => new Book.V1.Create
            {
                Title = Convert.ToBase64String(RandomNumberGenerator.GetBytes(150)),
                Author = author,
                Genre = Book.V1.Genre.Fiction,
            })
            .ToArray());
    }

    private static async Task<long> _countBooksAsync(InMemorySampleDataContextFactory factory, string author)
    {
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var page = await context.ReadBooksAsync(new Book_SearchQuery.V1 { Author = author, Limit = 1 }).ConfigureAwait(false);
        return page.Count;
    }

    private static async Task _waitUntilAsync(Func<Task<bool>> condition, Func<string> diagnostics)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await condition().ConfigureAwait(false))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition did not hold within 10 seconds. " + diagnostics());
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private static string _describeQueue(InMemoryMessagingTransport transport, string queue)
    {
        return "Queue '" + queue + "': " + transport.GetPendingCount(queue).ToString(CultureInfo.InvariantCulture)
            + " pending; dead letters: "
            + string.Join("; ", transport.GetDeadLetters(queue).Select(static letter => letter.Reason + ": " + letter.Description));
    }

    private sealed class Process(Container container, ServiceProvider provider, IReadOnlyList<IHostedService> hosted)
        : IAsyncDisposable
    {
        public Container Container { get; } = container;

        public async ValueTask DisposeAsync()
        {
            foreach (var service in hosted.Reverse())
                await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await provider.DisposeAsync().ConfigureAwait(false);
            await Container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class AllScopesUser : IContextProvider<ClaimsPrincipal>
    {
        private static readonly string[] _allScopes = typeof(ApplicationScopes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static field => (string)field.GetRawConstantValue()!)
            .ToArray();

        public ClaimsPrincipal Current { get; } = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "web-test-user"),
                new Claim("scope", string.Join(' ', _allScopes)),
            ],
            "IntegrationTests"));
    }
}
