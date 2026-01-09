// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Nodatime.Intervals;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "byte storage is sufficient")]
public enum TimePeriod : byte
{
    None,
    Hour = 2,
    TenMinutes = 3,
    Minute = 4,
    QuarterHour = 5, // unordered because added later
    HalfHour = 6, // unordered because added later
}