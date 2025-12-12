// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Json;

using EnsureThat;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using NodaTime;
using NodaTime.Serialization.JsonNet;

using System;
using System.Collections.Generic;

using System.Linq;

namespace Ark.Tools.Nodatime
{
    public static class Ex
    {
        [Obsolete("Use Ark.Tools.NewtonsoftJson ConfigureForArkDefaults()", true)]
        public static JsonSerializerSettings ConfigureForArkDefault(this JsonSerializerSettings settings)
        {
            settings.NullValueHandling = NullValueHandling.Include;
            settings.TypeNameHandling = TypeNameHandling.None;
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            settings.ConfigureForNodaTimeRanges();
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        public static JsonSerializerSettings ConfigureForNodaTimeRanges(this JsonSerializerSettings settings)
        {
            EnsureArg.IsNotNull(settings);


            if (!settings.Converters.Any(x => x == NodaConverters.LocalDateConverter))
                throw new InvalidOperationException("Missing NodaTime converters. Call 'ConfigureForNodaTime()' before 'ConfigureForNodaTimeRanges()'");

            // Add our converters
            AddDefaultConverters(settings.Converters);

            // return to allow fluent chaining if desired
            return settings;
        }

        public static JsonSerializer ConfigureForNodaTimeRanges(this JsonSerializer serializer)
        {
            EnsureArg.IsNotNull(serializer);


            if (!serializer.Converters.Any(x => x == NodaConverters.LocalDateConverter))
                throw new InvalidOperationException("Missing NodaTime converters. Call 'ConfigureForNodaTime()' before 'ConfigureForNodaTimeRanges()'");

            // Add our converters
            AddDefaultConverters(serializer.Converters);

            // return to allow fluent chaining if desired
            return serializer;
        }

        private static void AddDefaultConverters(IList<JsonConverter> converters)
        {
            EnsureArg.IsNotNull(converters);

            converters.Add(new LocalDateRangeConverter());
            converters.Add(new LocalDateTimeRangeConverter());
            converters.Add(new ZonedDateTimeRangeConverter());
        }
    }
}
