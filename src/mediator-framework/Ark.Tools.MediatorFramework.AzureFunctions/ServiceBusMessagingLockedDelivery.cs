// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.ObjectModel;

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;

using Microsoft.Azure.Functions.Worker;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

internal sealed class ServiceBusMessagingLockedDelivery : IMessagingLockedDelivery
{
    private const int _maximumDeadLetterReasonLength = 256;
    private const int _maximumDeadLetterDescriptionLength = 1_024;

    private readonly ServiceBusReceivedMessage _message;
    private readonly ServiceBusMessageActions _actions;
    private readonly IReadOnlyDictionary<string, string> _headers;

    public ServiceBusMessagingLockedDelivery(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _headers = new ReadOnlyDictionary<string, string>(
            message.ApplicationProperties.ToDictionary(
                static pair => pair.Key,
                static pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, string> Headers => _headers;

    public ReadOnlySequence<byte> Payload => new(_message.Body.ToMemory());

    public int DeliveryCount => checked((int)_message.DeliveryCount);

    public async Task RenewLockAsync(CancellationToken ctk)
    {
        await _actions.RenewMessageLockAsync(_message, ctk).ConfigureAwait(false);
    }

    public async Task CompleteAsync(CancellationToken ctk)
    {
        await _actions.CompleteMessageAsync(_message, ctk).ConfigureAwait(false);
    }

    public async Task AbandonAsync(CancellationToken ctk)
    {
        await _actions.AbandonMessageAsync(
            _message,
            cancellationToken: ctk).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(
        string reason,
        string description,
        CancellationToken ctk)
    {
        await _actions.DeadLetterMessageAsync(
            _message,
            deadLetterReason: _bound(reason, _maximumDeadLetterReasonLength),
            deadLetterErrorDescription: _bound(description, _maximumDeadLetterDescriptionLength),
            cancellationToken: ctk).ConfigureAwait(false);
    }

    private static string _bound(string value, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
