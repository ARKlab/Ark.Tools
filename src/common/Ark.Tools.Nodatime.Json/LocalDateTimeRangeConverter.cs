// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using System;
using System.Globalization;

namespace Ark.Tools.Nodatime.Json
{
    public class LocalDateRangeConverter : JsonConverter
    {
        private static readonly Type _type = typeof(LocalDateRange);
        private static readonly Type _nullableType = typeof(Nullable<LocalDateRange>);

        public override bool CanConvert(Type objectType)
        {
            return objectType == _type || objectType == _nullableType;
        }

        private sealed class Surrogate
        {
            public LocalDate Start { get; set; }
            public LocalDate End { get; set; }
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
                    throw new JsonReaderException(string.Format(CultureInfo.InvariantCulture, "Cannot convert null value to {0}.", objectType));
                }
                return null;
            }

            var s = jo.ToObject<Surrogate>(serializer);
            if (s == null) return null;

            return new LocalDateRange(s.Start, s.End);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                throw new JsonWriterException(nameof(value));
            }

            if (!(value is LocalDateRange || value is Nullable<LocalDateRange>))
            {
                throw new JsonWriterException(string.Format(CultureInfo.InvariantCulture, "Unexpected value when converting. Expected {0}, got {1}.", typeof(LocalDateRange).FullName, value.GetType().FullName));
            }

            LocalDateRange? r = null;

            if (value is Nullable<LocalDateRange>)
                r = value as Nullable<LocalDateRange>;


            if (value is LocalDateRange)
            {
                r = (LocalDateRange)value;
            }

            if (r.HasValue)
            {
                serializer.Serialize(writer, new Surrogate { Start = r.Value.Start, End = r.Value.End });
            }
            else
            {
                writer.WriteNull();
            }


        }
    }
}