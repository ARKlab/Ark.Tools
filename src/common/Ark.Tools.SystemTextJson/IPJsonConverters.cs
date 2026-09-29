// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>Converts <see cref="IPAddress"/> to and from its string representation.</summary>
public sealed class IPAddressJsonConverter : JsonConverter<IPAddress>
{
    /// <inheritdoc />
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && IPAddress.TryParse(reader.GetString(), out var address))
            return address;

        throw new JsonException($"The JSON value could not be converted to {typeof(IPAddress)}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Converts <see cref="IPEndPoint"/> to and from <c>address:port</c> (<c>[address]:port</c> for IPv6).
/// A missing port is read as <c>0</c>.
/// </summary>
public sealed class IPEndPointJsonConverter : JsonConverter<IPEndPoint>
{
    /// <inheritdoc />
    public override IPEndPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && IPEndPoint.TryParse(reader.GetString()!, out var endPoint))
            return endPoint;

        throw new JsonException($"The JSON value could not be converted to {typeof(IPEndPoint)}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IPEndPoint value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.ToString());
    }
}
