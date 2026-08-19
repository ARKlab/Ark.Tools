// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Messaging;

/// <summary>Names of headers used by the transport-neutral messaging envelope.</summary>
public static class MessagingHeaderNames
{
    /// <summary>AMF contract logical name header.</summary>
    public const string MessageType = "amf1-msg-type";

    /// <summary>AMF content type header.</summary>
    public const string ContentType = "amf1-content-type";

    /// <summary>AMF content encoding header.</summary>
    public const string ContentEncoding = "amf1-content-encoding";

    /// <summary>AMF message identifier header.</summary>
    public const string MessageId = "amf1-msg-id";

    /// <summary>AMF correlation identifier header.</summary>
    public const string CorrelationId = "amf1-corr-id";

    /// <summary>AMF sent-time header.</summary>
    public const string SentTime = "amf1-senttime";

    /// <summary>AMF network identity header.</summary>
    public const string Network = "amf1-network";

    /// <summary>AMF sender identity header.</summary>
    public const string SenderIdentity = "amf1-sender-identity";

    /// <summary>AMF DataBus attachment identifier header.</summary>
    public const string PayloadAttachmentId = "amf1-payload-attachment-id";

    /// <summary>AMF DataBus attachment length header.</summary>
    public const string PayloadAttachmentLength = "amf1-payload-attachment-length";

    /// <summary>AMF DataBus attachment SHA-256 header.</summary>
    public const string PayloadAttachmentSha256 = "amf1-payload-attachment-sha256";

    /// <summary>Rebus-compatible message type header.</summary>
    public const string RebusMessageType = "rbs2-msg-type";

    /// <summary>Rebus-compatible content type header.</summary>
    public const string RebusContentType = "rbs2-content-type";

    /// <summary>Rebus-compatible content encoding header.</summary>
    public const string RebusContentEncoding = "rbs2-content-encoding";

    /// <summary>Rebus-compatible message identifier header.</summary>
    public const string RebusMessageId = "rbs2-msg-id";

    /// <summary>Rebus-compatible correlation identifier header.</summary>
    public const string RebusCorrelationId = "rbs2-corr-id";

    /// <summary>Rebus-compatible sent-time header.</summary>
    public const string RebusSentTime = "rbs2-senttime";

    /// <summary>Rebus-compatible delivery-count header. Transports must not emit it in an envelope.</summary>
    public const string RebusDeliveryCount = "rbs2-delivery-count";

    /// <summary>Rebus-compatible failure step header.</summary>
    public const string RebusFailureStep = "rbs2-failure-step";

    /// <summary>Rebus-compatible failure exception type header.</summary>
    public const string RebusFailureType = "rbs2-exception-type";

    /// <summary>Rebus-compatible failure message header.</summary>
    public const string RebusFailureMessage = "rbs2-exception-message";
}

/// <summary>Rebus-compatible content types supported by the messaging runtime.</summary>
public static class MessagingContentTypes
{
    /// <summary>UTF-8 JSON content type.</summary>
    public const string Json = "application/json;charset=utf-8";

    /// <summary>MessagePack content type.</summary>
    public const string MessagePack = "application/x-msgpack";

    /// <summary>Protocol Buffers content type.</summary>
    public const string Protobuf = "application/x-protobuf";
}
