// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>
/// Marks a contract property carrying an opaque concurrency token. The property remains a normal,
/// fully serialized member on JSON, MessagePack, protobuf, and Rebus transports. On an HTTP request
/// contract, an <c>If-Match</c> header overrides the body value when present; on a response contract,
/// the value is also emitted as the <c>ETag</c> response header by the Minimal API transport.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ETagAttribute : Attribute
{
}
