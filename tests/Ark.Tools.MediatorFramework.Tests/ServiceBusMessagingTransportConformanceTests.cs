// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Runs the transport conformance suite against an explicitly configured Service Bus namespace.</summary>
[TestClass]
[TestCategory("integration")]
public sealed class ServiceBusMessagingTransportConformanceTests : MessagingTransportConformanceTests
{
    private const string _connectionVariable = "ARK_SERVICEBUS_CONNECTION_STRING";
    private const string _emulatorConnectionVariable = "ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING";
    private const string _queueVariable = "ARK_SERVICEBUS_QUEUE";
    private const string _emptyQueueVariable = "ARK_SERVICEBUS_EMPTY_QUEUE";
    private readonly List<ServiceBusMessagingTransport> _transports = new();
    private ServiceBusAdministrationClient? _administration;
    private string _connectionString = string.Empty;
    private string _queueName = string.Empty;
    private string _emptyQueueName = string.Empty;

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
        var connectionString = Environment.GetEnvironmentVariable(_connectionVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _connectionString = connectionString;
            _queueName = _requiredEnvironmentVariable(_queueVariable);
            _emptyQueueName = _requiredEnvironmentVariable(_emptyQueueVariable);
            return;
        }

        var emulatorConnectionString = _requiredEnvironmentVariable(_emulatorConnectionVariable);
        _connectionString = _withoutEndpointPort(emulatorConnectionString);
        var suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        _queueName = "ark-mf-conformance-" + suffix;
        _emptyQueueName = _queueName + "-empty";
        _administration = new ServiceBusAdministrationClient(emulatorConnectionString);
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

        if (_administration is null)
            return;

        await _administration.DeleteQueueAsync(_queueName).ConfigureAwait(false);
        await _administration.DeleteQueueAsync(_emptyQueueName).ConfigureAwait(false);
    }

    private static string _requiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        Assert.Inconclusive(
            $"Set {_emulatorConnectionVariable}, or set {_connectionVariable} and provision the queues "
            + $"named by {_queueVariable} and {_emptyQueueVariable}, to run Azure Service Bus conformance tests.");
        throw new InvalidOperationException("Assert.Inconclusive did not terminate the test.");
    }

    private static string _withoutEndpointPort(string connectionString)
    {
        const string endpointPrefix = "Endpoint=";
        var endpointStart = connectionString.IndexOf(endpointPrefix, StringComparison.Ordinal);
        if (endpointStart < 0)
            throw new FormatException("The Service Bus emulator connection string has no Endpoint.");

        endpointStart += endpointPrefix.Length;
        var endpointEnd = connectionString.IndexOf(';', endpointStart);
        if (endpointEnd < 0)
            endpointEnd = connectionString.Length;

        var endpoint = new UriBuilder(connectionString[endpointStart..endpointEnd])
        {
            Port = -1
        };
        return connectionString[..endpointStart] + endpoint.Uri.AbsoluteUri + connectionString[endpointEnd..];
    }
}
