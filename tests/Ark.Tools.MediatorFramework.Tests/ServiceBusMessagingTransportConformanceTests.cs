// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Runs the transport conformance suite against an explicitly configured Service Bus namespace.</summary>
[TestClass]
[TestCategory("integration")]
public sealed class ServiceBusMessagingTransportConformanceTests : MessagingTransportConformanceTests
{
    private const string _connectionVariable = "ARK_SERVICEBUS_CONNECTION_STRING";
    private const string _queueVariable = "ARK_SERVICEBUS_QUEUE";
    private const string _emptyQueueVariable = "ARK_SERVICEBUS_EMPTY_QUEUE";
    private readonly List<ServiceBusMessagingTransport> _transports = new();

    protected override string QueueName => _requiredEnvironmentVariable(_queueVariable);

    protected override string EmptyQueueName => _requiredEnvironmentVariable(_emptyQueueVariable);

    protected override IMessagingReceiveTransport CreateTransport()
    {
        var connectionString = _requiredEnvironmentVariable(_connectionVariable);
#pragma warning disable CA2000 // The tracked transport owns and disposes the client during test cleanup.
        var transport = new ServiceBusMessagingTransport(new ServiceBusClient(connectionString));
#pragma warning restore CA2000
        _transports.Add(transport);
        return transport;
    }

    [TestCleanup]
    public async Task DisposeTransports()
    {
        foreach (var transport in _transports)
            await transport.DisposeAsync().ConfigureAwait(false);
        _transports.Clear();
    }

    private static string _requiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        Assert.Inconclusive(
            $"Set {name} and provision the queues named by {_queueVariable} and {_emptyQueueVariable} "
            + "to run Azure Service Bus conformance tests.");
        throw new InvalidOperationException("Assert.Inconclusive did not terminate the test.");
    }
}
