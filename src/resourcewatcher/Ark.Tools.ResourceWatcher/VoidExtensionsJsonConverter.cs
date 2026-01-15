// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// JSON converter for <see cref="VoidExtensions"/> that serializes to/from null.
/// </summary>
public class VoidExtensionsJsonConverter : JsonConverter<VoidExtensions>
{
    /// <inheritdoc/>
    public override VoidExtensions? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, VoidExtensions? value, JsonSerializerOptions options)
    {
        writer.WriteNullValue();
    }
}
