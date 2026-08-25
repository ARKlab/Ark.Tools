// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;

namespace Ark.Tools.MediatorFramework;

/// <summary>Default immutable implementation of an inline messaging failure.</summary>
/// <typeparam name="T">The original message type.</typeparam>
public sealed class MessagingFailed<T> : IFailed<T>
    where T : class
{
    /// <summary>Creates an inline failure snapshot.</summary>
    /// <param name="message">The original message.</param>
    /// <param name="deliveryCount">The native delivery count.</param>
    /// <param name="exceptions">The bounded exception snapshots.</param>
    public MessagingFailed(
        T message,
        int deliveryCount,
        IReadOnlyList<MessagingExceptionInfo> exceptions)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(deliveryCount, 1);
        ArgumentNullException.ThrowIfNull(exceptions);
        if (exceptions.Count == 0)
            throw new ArgumentException("At least one exception is required.", nameof(exceptions));

        Message = message;
        DeliveryCount = deliveryCount;
        Exceptions = new ReadOnlyCollection<MessagingExceptionInfo>(exceptions.ToArray());
        ErrorDescription = Exceptions[0].Message;
    }

    /// <inheritdoc />
    public T Message { get; }

    /// <inheritdoc />
    public int DeliveryCount { get; }

    /// <inheritdoc />
    public string ErrorDescription { get; }

    /// <inheritdoc />
    public IReadOnlyList<MessagingExceptionInfo> Exceptions { get; }
}
