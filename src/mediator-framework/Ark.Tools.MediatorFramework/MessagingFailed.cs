// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Collections.ObjectModel;

namespace Ark.Tools.MediatorFramework;

/// <summary>
/// Represents an immutable inline messaging failure command handled through the
/// standard <see cref="ICommandHandler{TCommand}"/> pipeline.
/// </summary>
/// <typeparam name="T">The original message type.</typeparam>
public sealed class MessagingFailed<T> : ICommand<MessagingFailed<T>>
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

    /// <summary>Gets the original message.</summary>
    public T Message { get; }

    /// <summary>Gets the native delivery count when the failure was captured.</summary>
    public int DeliveryCount { get; }

    /// <summary>Gets the bounded error description.</summary>
    public string ErrorDescription { get; }

    /// <summary>Gets the serializable exception snapshots.</summary>
    public IReadOnlyList<MessagingExceptionInfo> Exceptions { get; }
}
