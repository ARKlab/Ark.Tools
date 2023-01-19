// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using System;

namespace Ark.Tools.Nodatime.Json
{
    public class LocalDateTimeRangeConverter : JsonConverter
    {
        private static readonly Type _type = typeof(LocalDateTimeRange);
        private static readonly Type _nullableType = typeof(Nullable<LocalDateTimeRange>);

        public override bool CanConvert(Type objectType)
        {
            return objectType == _type || objectType == _nullableType;
        }

        private class Surrogate
        {
            public LocalDateTime Start { get; set; }
            public LocalDateTime End { get; set; }
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {

            if (reader.TokenType == JsonToken.Null)
            {
                if (objectType != _nullableType)
                {
                    throw new JsonReaderException(string.Format("Cannot convert null value to {0}.", objectType));
                }
                return null;
            }

            var jo = JObject.Load(reader);

            if (jo == null)
            {
                if (objectType != _nullableType)
                {
                    throw new JsonReaderException(string.Format("Cannot convert null value to {0}.", objectType));
                }
                return null;
            }

            var s = jo.ToObject<Surrogate>(serializer);
            if (s == null) return null;

            return new LocalDateTimeRange(s.Start, s.End);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!(value is LocalDateTimeRange || value is Nullable<LocalDateTimeRange>))
            {
                throw new ArgumentException(string.Format("Unexpected value when converting. Expected {0}, got {1}.", typeof(LocalDateTimeRange).FullName, value.GetType().FullName));
            }

            LocalDateTimeRange? r = null;

            if (value is Nullable<LocalDateTimeRange>)
                r = value as Nullable<LocalDateTimeRange>;

            if (value is LocalDateTimeRange)
            {
                r = (LocalDateTimeRange)value;
            }

            if (r.HasValue)
            {
                serializer.Serialize(writer, new Surrogate { Start = r.Value.Start, End = r.Value.End });
            } else
            {
                writer.WriteNull();
            }
        }
    }
}
