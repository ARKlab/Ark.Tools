// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using Azure.Messaging.ServiceBus.Administration;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>The local Service Bus emulator, read as <c>ServiceBusMessagingTransportConformanceTests</c> does.</summary>
internal static class ServiceBusEmulator
{
    [InfrastructureSecret]
    private const string _defaultAdministrationConnectionString = "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    /// <summary>The administration endpoint (HTTP port 5300): use with <see cref="ServiceBusAdministrationClient"/>.</summary>
    [InfrastructureSecret]
    public static readonly string AdministrationConnectionString = _serviceBusConnectionString();

    /// <summary>The data-plane endpoint (no port): use with <c>ServiceBusClient</c>.</summary>
    [InfrastructureSecret]
    public static readonly string DataPlaneConnectionString = _dataPlaneConnectionString(AdministrationConnectionString);

    /// <summary>Deletes the queue if it exists, then creates it empty.</summary>
    /// <param name="administration">The administration client.</param>
    /// <param name="queue">The queue name.</param>
    /// <returns>A task that completes when the queue exists and is empty.</returns>
    public static async Task RecreateQueueAsync(ServiceBusAdministrationClient administration, string queue)
    {
        ArgumentNullException.ThrowIfNull(administration);
        if ((await administration.QueueExistsAsync(queue).ConfigureAwait(false)).Value)
            await administration.DeleteQueueAsync(queue).ConfigureAwait(false);
        await administration.CreateQueueAsync(queue).ConfigureAwait(false);
    }

    private static string _serviceBusConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        return _defaultAdministrationConnectionString;
    }

    [ComplianceReviewed("ARKPII005", "The emulator connection string is rewritten locally to reach the data plane and is never logged.")]
    private static string _dataPlaneConnectionString([InfrastructureSecret] string connectionString)
    {
        const string endpointPrefix = "Endpoint=";
        var endpointStart = connectionString.IndexOf(endpointPrefix, StringComparison.Ordinal)
            + endpointPrefix.Length;
        var endpointEnd = endpointStart + connectionString.AsSpan(endpointStart).IndexOf(';');
        var endpoint = new Uri(connectionString[endpointStart..endpointEnd]);
        var dataPlaneEndpoint = new UriBuilder(endpoint) { Port = -1 }.Uri
            .AbsoluteUri.TrimEnd('/');
        return connectionString[..endpointStart] + dataPlaneEndpoint + connectionString[endpointEnd..];
    }
}
