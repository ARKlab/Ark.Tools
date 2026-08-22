// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Names of headers used by the transport-neutral messaging runtime.</summary>
public static class MessagingHeaders
{
    /// <summary>Rebus-compatible message type header.</summary>
    public const string RebusType = "rbs2-msg-type";

    /// <summary>Rebus-compatible message identifier header.</summary>
    public const string RebusMessageId = "rbs2-msg-id";

    /// <summary>Rebus-compatible correlation identifier header.</summary>
    public const string RebusCorrelationId = "rbs2-corr-id";

    /// <summary>Rebus-compatible sent-time header.</summary>
    public const string RebusSentTime = "rbs2-senttime";

    /// <summary>Rebus-compatible delivery-count header.</summary>
    public const string RebusDeliveryCount = "rbs2-delivery-count";

    /// <summary>Rebus-compatible content-type header.</summary>
    public const string RebusContentType = "rbs2-content-type";

    /// <summary>Rebus-compatible error-details header.</summary>
    public const string RebusErrorDetails = "rbs2-error-details";

    /// <summary>Logical messaging contract name.</summary>
    public const string MessageType = "amf1-msg-type";

    /// <summary>Message content type.</summary>
    public const string ContentType = "amf1-content-type";

    /// <summary>Optional content encoding.</summary>
    public const string ContentEncoding = "amf1-content-encoding";

    /// <summary>Message identifier.</summary>
    public const string MessageId = "amf1-msg-id";

    /// <summary>Correlation identifier.</summary>
    public const string CorrelationId = "amf1-corr-id";

    /// <summary>Message sent time.</summary>
    public const string SentTime = "amf1-senttime";

    /// <summary>Resolved producer network identity.</summary>
    public const string Network = "amf1-network";

    /// <summary>Identity of the participant that sent the message.</summary>
    public const string SenderIdentity = "amf1-sender-identity";

    /// <summary>Identifier of an offloaded payload attachment.</summary>
    public const string PayloadAttachmentId = "amf1-payload-attachment-id";

    /// <summary>Length of an offloaded payload attachment.</summary>
    public const string PayloadAttachmentLength = "amf1-payload-attachment-length";

    /// <summary>SHA-256 digest of an offloaded payload attachment.</summary>
    public const string PayloadAttachmentSha256 = "amf1-payload-attachment-sha256";

    /// <summary>User authentication type header.</summary>
    public const string UserAuthenticationType = "ark-auth-type";

    /// <summary>User identifier header.</summary>
    public const string UserId = "ark-user-id";

    /// <summary>User email header.</summary>
    public const string UserEmail = "ark-user-email";

    /// <summary>User scopes header.</summary>
    public const string UserScopes = "ark-user-scopes";

    /// <summary>User roles header.</summary>
    public const string UserRoles = "ark-user-roles";
}
