// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Json;

using EnsureThat;

using Newtonsoft.Json;

using NodaTime.Serialization.JsonNet;

using System;
using System.Collections.Generic;

using System.Linq;

namespace Ark.Tools.Nodatime
{
    public static class Ex
    {

        public static JsonSerializerSettings ConfigureForNodaTimeRanges(this JsonSerializerSettings settings)
        {
            EnsureArg.IsNotNull(settings);


            if (!settings.Converters.Any(x => x == NodaConverters.LocalDateConverter))
                throw new InvalidOperationException("Missing NodaTime converters. Call 'ConfigureForNodaTime()' before 'ConfigureForNodaTimeRanges()'");

            // Add our converters
            _addDefaultConverters(settings.Converters);

            // return to allow fluent chaining if desired
            return settings;
        }

        public static JsonSerializer ConfigureForNodaTimeRanges(this JsonSerializer serializer)
        {
            EnsureArg.IsNotNull(serializer);


            if (!serializer.Converters.Any(x => x == NodaConverters.LocalDateConverter))
                throw new InvalidOperationException("Missing NodaTime converters. Call 'ConfigureForNodaTime()' before 'ConfigureForNodaTimeRanges()'");

            // Add our converters
            _addDefaultConverters(serializer.Converters);

            // return to allow fluent chaining if desired
            return serializer;
        }

        private static void _addDefaultConverters(IList<JsonConverter> converters)
        {
            EnsureArg.IsNotNull(converters);

            converters.Add(new LocalDateRangeConverter());
            converters.Add(new LocalDateTimeRangeConverter());
            converters.Add(new ZonedDateTimeRangeConverter());
        }
    }
}