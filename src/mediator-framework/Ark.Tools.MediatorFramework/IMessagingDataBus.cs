// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Stores streamed payloads outside the transport and provides bounded reads.</summary>
public interface IMessagingDataBus
{
    /// <summary>Opens a transactional attachment writer.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A write session that commits or aborts the attachment.</returns>
    Task<IMessagingDataBusWriteSession> OpenWriteAsync(CancellationToken ctk);

    /// <summary>
    /// Opens an attachment whose length and SHA-256 digest are validated while reading,
    /// with validation completing at end-of-stream.
    /// </summary>
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

    /// <summary>Deletes a committed attachment when its enclosing message cannot be sent.</summary>
    /// <param name="attachmentId">The opaque attachment identifier.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task DeleteAsync(string attachmentId, CancellationToken ctk);
}

/// <summary>Writes one DataBus attachment and commits its integrity metadata.</summary>
public interface IMessagingDataBusWriteSession : IAsyncDisposable
{
    /// <summary>Gets the forward-only attachment destination.</summary>
    Stream Stream { get; }

    /// <summary>Commits the attachment after all bytes have been written.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The committed attachment metadata.</returns>
    Task<MessagingDataBusAttachment> CompleteAsync(CancellationToken ctk);
}

/// <summary>Identifies a committed DataBus attachment and its integrity metadata.</summary>
/// <param name="Id">The opaque attachment identifier.</param>
/// <param name="Length">The stored byte length.</param>
/// <param name="Sha256">The uppercase SHA-256 digest.</param>
public sealed record MessagingDataBusAttachment(string Id, long Length, string Sha256);
