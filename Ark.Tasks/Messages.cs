﻿// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Text;

using System;
using System.Diagnostics;
using System.Globalization;

namespace Ark.Tasks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct Resource : IEquatable<Resource>
    {
        public Resource(string provider, string id)
        {
            Provider = provider;
            Id = id;
        }

        public string Provider;
        public string Id;

        public static Resource Create(string provider, string resourceId)
        {
            return new Resource(provider, resourceId);
        }

        public readonly bool Equals(Resource other)
        {
            return string.Equals(Provider, other.Provider, StringComparison.InvariantCultureIgnoreCase)
                && string.Equals(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool operator ==(Resource x, Resource y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Resource x, Resource y)
        {
            return !x.Equals(y);
        }

        public override readonly bool Equals(object? obj)
        {
            if (!(obj is Resource resource))
                return false;

            return Equals(resource);
        }

        public override readonly int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + StringComparer.OrdinalIgnoreCase.GetHashCode(Provider);
                hash = hash * 92821 + StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
                return hash;
            }
        }

        public override readonly string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", Provider, Id);
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }
    }

    internal sealed class ZonedDateTimeTzdbConverter : JsonConverter
    {
        private readonly JsonConverter _converter = new NodaPatternConverter<ZonedDateTime>(
                ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFFFo<G> z", DateTimeZoneProviders.Tzdb)
            , x =>
            {
                if (x.Calendar != CalendarSystem.Iso) throw new InvalidOperationException("Invalid Calendar for ZonedDateTime");
            }
            );

        public override bool CanRead => _converter.CanRead;
        public override bool CanWrite => _converter.CanWrite;

        public override bool CanConvert(Type objectType)
            => _converter.CanConvert(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            => _converter.ReadJson(reader, objectType, existingValue, serializer);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => _converter.WriteJson(writer, value, serializer);
    }

    public struct Slice : IEquatable<Slice>
    {
        internal Slice(ZonedDateTime start)
        {
            SliceStart = start;
        }

        [JsonConverter(typeof(ZonedDateTimeTzdbConverter))]
        public ZonedDateTime SliceStart;

        public readonly Slice MoveDays(int days)
        {
            return Slice.From(SliceStart.LocalDateTime.PlusDays(days).InZoneLeniently(SliceStart.Zone));
        }

        public readonly Slice MoveAtStartOfWeek(IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Monday)
        {
            return Slice.From(SliceStart.LocalDateTime.Date.FirstDayOfTheWeek(dayOfWeek).AtMidnight().InZoneLeniently(SliceStart.Zone));
        }

        public readonly Slice MoveAtStartOfMonth()
        {
            return Slice.From(SliceStart.LocalDateTime.Date.FirstDayOfTheMonth().AtMidnight().InZoneLeniently(SliceStart.Zone));
        }

        public static Slice From(ZonedDateTime start)
        {
            return new Slice(start);
        }

        public static Slice From(LocalDate start, string timezone)
        {
            return new Slice(start.AtMidnight().InZoneStrictly(DateTimeZoneProviders.Tzdb[timezone]));
        }

        public readonly bool Equals(Slice other)
        {
            return SliceStart == other.SliceStart;
        }

        public static bool operator ==(Slice x, Slice y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Slice x, Slice y)
        {
            return !x.Equals(y);
        }

        public override readonly bool Equals(object? obj)
        {
            if (!(obj is Slice))
                return false;

            return Equals((Slice)obj);
        }

        public override readonly int GetHashCode()
        {
            return SliceStart.GetHashCode();
        }

        public override readonly string ToString()
        {
            return SliceStart.ToString("F", null);
        }
    }
}

namespace Ark.Tasks.Messages
{
    public class ResourceSliceReady
    {
        public Resource Resource { get; set; }
        public Slice Slice { get; set; }
    }

    public class SliceReady : IEquatable<SliceReady>
    {
        public Resource Resource { get; set; }

        public Slice ResourceSlice { get; set; }

        public Slice ActivitySlice { get; set; }

        public bool Equals(SliceReady? other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (Equals(other, null))
                return false;

            return Resource == other.Resource
                && ResourceSlice == other.ResourceSlice
                && ActivitySlice == other.ActivitySlice;
        }

        public static bool operator ==(SliceReady x, SliceReady y)
        {
            if (!Equals(x, null))
                return x.Equals(y);
            else if (Equals(y, null))
                return true;
            else
                return false;

        }

        public static bool operator !=(SliceReady x, SliceReady y)
        {
            if (!Equals(x, null))
                return !x.Equals(y);
            else if (Equals(y, null))
                return false;
            else
                return true;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is SliceReady))
                return false;

            return Equals((SliceReady)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + Resource.GetHashCode();
                hash = hash * 92821 + ResourceSlice.GetHashCode();
                hash = hash * 92821 + ActivitySlice.GetHashCode();
                return hash;
            }
        }
    }
}
