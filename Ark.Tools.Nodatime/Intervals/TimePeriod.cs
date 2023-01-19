// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Nodatime.Intervals
{
    public enum TimePeriod : byte
    {
        Hour = 2,
        TenMinutes = 3,
        Minute = 4,
        QuarterHour = 5, // unordered because added later
        HalfHour = 6, // unordered because added later
    }
}
