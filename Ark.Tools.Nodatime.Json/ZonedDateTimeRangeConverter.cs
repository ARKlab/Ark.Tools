// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using System;

namespace Ark.Tools.Nodatime.Json
{
    
    public class ZonedDateTimeRangeConverter : JsonConverter
    {
        private static readonly Type _type = typeof(ZonedDateTimeRange);
        private static readonly Type _nullableType = typeof(Nullable<ZonedDateTimeRange>);

        public override bool CanConvert(Type objectType)
        {
            return objectType == _type || objectType == _nullableType;
        }

        private class Surrogate
        {
            public ZonedDateTime Start { get; set; }
            public ZonedDateTime End { get; set; }
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
            
            return new ZonedDateTimeRange(s.Start, s.End); 
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!(value is ZonedDateTimeRange || value is Nullable<ZonedDateTimeRange>))
            {
                throw new ArgumentException(string.Format("Unexpected value when converting. Expected {0}, got {1}.", typeof(ZonedDateTimeRange).FullName, value.GetType().FullName));
            }

            ZonedDateTimeRange? r = null;

            if (value is Nullable<ZonedDateTimeRange>)
                r = value as Nullable<ZonedDateTimeRange>;


            if (value is ZonedDateTimeRange)
            {
                r = (ZonedDateTimeRange)value;
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
