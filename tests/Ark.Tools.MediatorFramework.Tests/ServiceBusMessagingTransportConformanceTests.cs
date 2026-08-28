// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Runs the transport conformance suite against an explicitly configured Service Bus namespace.</summary>
[TestClass]
[TestCategory("integration")]
[DoNotParallelize]
public sealed class ServiceBusMessagingTransportConformanceTests : MessagingTransportConformanceTests
{
    private const string _administrationConnectionString = "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private const string _connectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private const string _queueName = "ark-mf-conformance";
    private const string _emptyQueueName = "ark-mf-conformance-empty";
    private readonly List<ServiceBusMessagingTransport> _transports = new();
    private readonly ServiceBusAdministrationClient _administration = new(_administrationConnectionString);

    protected override string QueueName => _queueName;

    protected override string EmptyQueueName => _emptyQueueName;

    protected override IMessagingReceiveTransport CreateTransport()
    {
#pragma warning disable CA2000 // The tracked transport owns and disposes the client during test cleanup.
        var transport = new ServiceBusMessagingTransport(new ServiceBusClient(_connectionString));
#pragma warning restore CA2000
        _transports.Add(transport);
        return transport;
    }

    [TestInitialize]
    public async Task InitializeQueues()
    {
        await _deleteQueuesAsync().ConfigureAwait(false);
        await _administration.CreateQueueAsync(_queueName).ConfigureAwait(false);
        try
        {
            await _administration.CreateQueueAsync(_emptyQueueName).ConfigureAwait(false);
        }
        catch
        {
            await _administration.DeleteQueueAsync(_queueName).ConfigureAwait(false);
            throw;
        }
    }

    [TestCleanup]
    public async Task DisposeTransports()
    {
        foreach (var transport in _transports)
            await transport.DisposeAsync().ConfigureAwait(false);
        _transports.Clear();

        await _deleteQueuesAsync().ConfigureAwait(false);
    }

    private async Task _deleteQueuesAsync()
    {
        if ((await _administration.QueueExistsAsync(_queueName).ConfigureAwait(false)).Value)
            await _administration.DeleteQueueAsync(_queueName).ConfigureAwait(false);
        if ((await _administration.QueueExistsAsync(_emptyQueueName).ConfigureAwait(false)).Value)
            await _administration.DeleteQueueAsync(_emptyQueueName).ConfigureAwait(false);
    }
}
