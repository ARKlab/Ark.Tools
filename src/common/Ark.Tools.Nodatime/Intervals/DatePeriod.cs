// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Nodatime.Intervals
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "storage as byte is sufficient")]
    public enum DatePeriod : byte
    {
        None,
        Day = 2,
        Week = 3,
        Month = 4,
        Bimestral = 5,
        Trimestral = 6,
        Calendar = 7
    }
}