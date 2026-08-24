// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework;

/// <summary>Stores payloads outside the transport and provides bounded reads.</summary>
public interface IMessagingDataBus
{
    /// <summary>Stores the exact final payload bytes.</summary>
    /// <param name="content">The payload to store.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>An opaque attachment identifier.</returns>
    Task<string> StoreAsync(ReadOnlySequence<byte> content, CancellationToken ctk);

    /// <summary>Opens an attachment after validating its length and SHA-256 digest.</summary>
    /// <param name="attachmentId">The opaque attachment identifier.</param>
    /// <param name="expectedLength">The expected stored byte length.</param>
    /// <param name="expectedSha256">The expected SHA-256 digest.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A stream containing the attachment bytes.</returns>
    Task<Stream> OpenReadAsync(
        string attachmentId,
        long expectedLength,
        string expectedSha256,
        CancellationToken ctk);
}
