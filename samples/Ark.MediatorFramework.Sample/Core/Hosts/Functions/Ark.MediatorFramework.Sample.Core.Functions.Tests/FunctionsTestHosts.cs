// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

extern alias Processor;

using Ark.MediatorFramework.Sample.Core.Functions.Hosting;
using Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using System.Reflection;
using System.Security.Claims;

using ProcessorFunctions = Processor::Ark.Tools.MediatorFramework.AzureFunctions.Generated.ArkGeneratedMessagingFunctions;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>Composes the Functions processes as their <c>Program.cs</c> do, over a real Service Bus client.</summary>
internal static class FunctionsTestHosts
{
    /// <summary>The queue the Processor app's generated trigger binds to.</summary>
    /// <returns>The worker queue name.</returns>
    public static string WorkerQueueName()
    {
        return ProcessorFunctions.Manifest.Queue;
    }

    /// <summary>Starts the Api app's producer with an authenticated user holding every scope.</summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="store">The application store, whose outbox the producer commits to.</param>
    /// <returns>The started Api process.</returns>
    public static async Task<FunctionsTestProcess> StartApiProducerAsync(
        ServiceBusClient client,
        InMemorySampleDataContextFactory store)
    {
        var container = FunctionsHosting.CreateContainer(new ApplicationOptions { DataContextFactory = store });
        var services = new ServiceCollection();
        services.AddLogging();
        // Registered before AddArkAzureFunctions, whose TryAdd would otherwise read the HTTP user.
        services.AddSingleton<IContextProvider<ClaimsPrincipal>>(new AllScopesUser());
        // The in-memory DataBus is enough: the test runs every process in one.
        FunctionsHosting.AddApiProducer(
            services,
            container,
            transport => transport.UseServiceBus(client),
            static dataBus => dataBus.UseInMemory(lifetime: Duration.FromHours(2)));
        return await FunctionsTestProcess.StartAsync(services, container).ConfigureAwait(false);
    }

    /// <summary>Starts the outbox processor over the same store.</summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="store">The application store whose outbox is drained.</param>
    /// <returns>The started outbox processor process.</returns>
    public static async Task<FunctionsTestProcess> StartOutboxProcessorAsync(
        ServiceBusClient client,
        InMemorySampleDataContextFactory store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
#pragma warning disable CA2000 // The test owns the Service Bus client the transport wraps.
        OutboxProcessorComposition.AddOutboxProcessor(services, new ServiceBusMessagingTransport(client), store);
#pragma warning restore CA2000
        return await FunctionsTestProcess.StartAsync(services, container: null).ConfigureAwait(false);
    }

    /// <summary>Creates a valid book.</summary>
    /// <returns>The create request.</returns>
    public static Book_CreateRequest.V1 NewBook()
    {
        return new Book_CreateRequest.V1(new Book.V1.Create
        {
            Title = "Dune",
            Author = "Herbert",
            Genre = Book.V1.Genre.Fiction,
        });
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
