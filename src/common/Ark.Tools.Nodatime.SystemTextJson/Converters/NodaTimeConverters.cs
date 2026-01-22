// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

internal static class NodaConverterValidators
{
    internal static Action<T> CreateIsoValidator<T>(Func<T, CalendarSystem> calendarProjection)
    {
        return value =>
        {
            var calendar = calendarProjection(value);
            if (calendar != CalendarSystem.Iso)
            {
                var message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Values of type {0} must (currently) use the ISO calendar in order to be serialized.",
                    typeof(T).Name);
                throw new ArgumentException(message);
            }
        };
    }
}