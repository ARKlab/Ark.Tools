using Ark.Tools.Core;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ark.Tools.NewtonsoftJson
{
    public sealed class ValueCollectionConverter : JsonConverter
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(ValueCollection<>);

        private JsonConverter _getConverter(Type typeToConvert)
        {
            var elementType = typeToConvert.GetGenericArguments()[0];

            var converterType = typeof(Converter<>);

            var converter = (JsonConverter)Activator.CreateInstance(
                converterType.MakeGenericType(elementType),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: null,
                culture: null)!;

            return converter;
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            return _getConverter(objectType).ReadJson(reader, objectType, existingValue, serializer);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                _getConverter(value.GetType()).WriteJson(writer, value, serializer);
            }
        }

        private class Converter<T> : JsonConverter<ValueCollection<T>>
        {
            public override ValueCollection<T>? ReadJson(JsonReader reader, Type objectType, ValueCollection<T>? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var l = serializer.Deserialize<IList<T?>>(reader);
                if (l == null) return null;

                return new ValueCollection<T>(l);
            }

            public override void WriteJson(JsonWriter writer, ValueCollection<T>? value, JsonSerializer serializer)
            {
                if (value is null)
                {
                    writer.WriteNull();
                } else
                {
                    writer.WriteStartArray();
                    foreach (var e in value)
                    {
                        serializer.Serialize(writer, e);
                    }
                    writer.WriteEndArray();
                }
            }
        }
    }
}
