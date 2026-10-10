// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

extern alias Processor;

using Ark.MediatorFramework.Sample.Core.Functions.Hosting;
using Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;

using ProcessorFunctions = Processor::Ark.Tools.MediatorFramework.AzureFunctions.Generated.ArkGeneratedMessagingFunctions;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>Composes the Functions processes as their <c>Program.cs</c> do.</summary>
internal static class FunctionsTestHosts
{
    /// <summary>The number of books in <see cref="NewOversizedBulkImport"/>.</summary>
    public const int BulkImportSize = 2_000;

    // Composition only: the namespace is not contacted; tests deliver messages to the trigger directly.
    private static readonly IConfiguration _triggerConfiguration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureServiceBus:ConnectionString"] = "sample.servicebus.windows.net",
        })
        .Build();

    /// <summary>The queue the Processor app's generated trigger binds to.</summary>
    /// <returns>The worker queue name.</returns>
    public static string WorkerQueueName()
    {
        return ProcessorFunctions.Manifest.Queue;
    }

    /// <summary>Starts the Api app's producer with an authenticated user holding every scope.</summary>
    /// <param name="transport">Selects the transport.</param>
    /// <param name="dataBus">Selects the claim-check DataBus.</param>
    /// <param name="store">The application store, whose outbox the producer commits to.</param>
    /// <returns>The started Api process.</returns>
    public static async Task<FunctionsTestProcess> StartApiProducerAsync(
        Action<MessagingTransportBuilder> transport,
        Action<MessagingDataBusBuilder> dataBus,
        InMemorySampleDataContextFactory store)
    {
        var container = FunctionsHosting.CreateContainer(new ApplicationOptions { DataContextFactory = store });
        var services = new ServiceCollection();
        services.AddLogging();
        // Registered before AddArkAzureFunctions, whose TryAdd would otherwise read the HTTP user.
        services.AddSingleton<IContextProvider<ClaimsPrincipal>>(new AllScopesUser());
        FunctionsHosting.AddApiProducer(services, container, transport, dataBus);
        return await FunctionsTestProcess.StartAsync(services, container).ConfigureAwait(false);
    }

    /// <summary>Composes the Processor app's Service Bus trigger; the test delivers messages to it.</summary>
    /// <param name="dataBus">The claim-check DataBus shared with the producer.</param>
    /// <param name="store">The application store.</param>
    /// <returns>The composed Processor process.</returns>
    public static FunctionsTestProcess ComposeWorkerTrigger(
        IMessagingDataBus dataBus,
        InMemorySampleDataContextFactory store)
    {
        var container = FunctionsHosting.CreateContainer(new ApplicationOptions { DataContextFactory = store });
        var services = new ServiceCollection();
        services.AddLogging();
        FunctionsHosting.AddMessagingTrigger(
            services,
            _triggerConfiguration,
            ProcessorFunctions.Manifest,
            container,
            builder => builder.Use(dataBus));
        return FunctionsTestProcess.Compose(services, container);
    }

    /// <summary>Starts the outbox processor over the same store.</summary>
    /// <param name="transport">The transport the outbox processor dispatches to.</param>
    /// <param name="store">The application store whose outbox is drained.</param>
    /// <returns>The started outbox processor process.</returns>
    public static async Task<FunctionsTestProcess> StartOutboxProcessorAsync(
        IMessagingTransport transport,
        InMemorySampleDataContextFactory store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        OutboxProcessorComposition.AddOutboxProcessor(services, transport, store);
        return await FunctionsTestProcess.StartAsync(services, container: null).ConfigureAwait(false);
    }

    /// <summary>Creates a valid book.</summary>
    /// <returns>The create request.</returns>
    public static Book_CreateRequest.V1 NewBook()
    {
        return new Book_CreateRequest.V1(new Book.V1.Create
        {
            Title = BookTitle.From("Dune"),
            Author = PersonName.From("Herbert"),
            Genre = Book.V1.Genre.Fiction,
        });
    }

    /// <summary>Creates a bulk import whose serialized body exceeds the Service Bus message limit.</summary>
    /// <param name="author">The author shared by every imported book, used to find them.</param>
    /// <returns>The bulk request.</returns>
    public static Book_BulkCreateRequest.V1 NewOversizedBulkImport(PersonName author)
    {
        // About 2,000 x 200 bytes of random Base64 titles, which compress poorly: the payload stays above the
        // offload threshold even if messaging compression is enabled.
        return new Book_BulkCreateRequest.V1(Enumerable.Range(0, BulkImportSize)
            .Select(_ => new Book.V1.Create
            {
                Title = BookTitle.From(Convert.ToBase64String(RandomNumberGenerator.GetBytes(150))),
                Author = author,
                Genre = Book.V1.Genre.Fiction,
            })
            .ToArray());
    }

    /// <summary>Counts the stored books written by an author.</summary>
    /// <param name="store">The application store.</param>
    /// <param name="author">The author.</param>
    /// <returns>The number of matching books.</returns>
    public static async Task<long> CountBooksAsync(InMemorySampleDataContextFactory store, PersonName author)
    {
        var context = await store.CreateAsync().ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var page = await context.ReadBooksAsync(new Book_SearchQuery.V1 { Author = author, Limit = 1 }).ConfigureAwait(false);
        return page.Count;
    }

    private sealed class AllScopesUser : IContextProvider<ClaimsPrincipal>
    {
        private static readonly string[] _allScopes = typeof(ApplicationScopes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static field => (string)field.GetRawConstantValue()!)
            .ToArray();

        public ClaimsPrincipal Current { get; } = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "functions-test-user"),
                new Claim("scope", string.Join(' ', _allScopes)),
            ],
            "IntegrationTests"));
    }
}
