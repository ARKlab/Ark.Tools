// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Restricted one-way bus used by messaging handlers.</summary>
public interface IBus
{
    /// <summary>Sends a message to its processing participant's identity queue.</summary>
    /// <typeparam name="T">The message contract type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="additionalHeaders">Optional application headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the message is accepted.</returns>
    Task Send<T>(
        T message,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Sends a message after a relative delay.</summary>
    /// <typeparam name="T">The message contract type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="delay">The delay before delivery.</param>
    /// <param name="additionalHeaders">Optional application headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the message is accepted.</returns>
    Task Defer<T>(
        T message,
        TimeSpan delay,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Sends a message at an absolute due time.</summary>
    /// <typeparam name="T">The message contract type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="dueTime">The UTC delivery time.</param>
    /// <param name="additionalHeaders">Optional application headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the message is accepted.</returns>
    Task Defer<T>(
        T message,
        DateTimeOffset dueTime,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Publishes an event to its publisher topic.</summary>
    /// <typeparam name="T">The event contract type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="additionalHeaders">Optional application headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the event is accepted.</returns>
    Task Publish<T>(
        T @event,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class;
}
