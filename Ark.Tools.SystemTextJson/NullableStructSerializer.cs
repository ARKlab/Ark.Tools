﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson
{
    public class NullableStructSerializer<TStruct> : JsonConverter<TStruct?>
        where TStruct : struct
    {
        public override TStruct? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            return JsonSerializer.Deserialize<TStruct>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TStruct? value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
                JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
