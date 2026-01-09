// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System.Collections.Generic;

namespace Ark.Tools.Core;

public class OffsetDateTimeNullableComparer : IComparer<OffsetDateTime?>, IEqualityComparer<OffsetDateTime?>
{
    public static readonly OffsetDateTimeNullableComparer Instance = new();

    public int Compare(OffsetDateTime? x, OffsetDateTime? y)
    {

        //Two nulls are equal
        if (!x.HasValue && !y.HasValue)
            return 0;

        //Any object is different than null
        if (!y.HasValue)
            return 1;

        if (!x.HasValue)
            return -1;

        //Otherwise compare the two values
        return OffsetDateTime.Comparer.Instant.Compare(x.Value, y.Value);
    }

    public bool Equals(OffsetDateTime? x, OffsetDateTime? y)
    {
        //Two nulls are equal
        if (!x.HasValue && !y.HasValue)
            return true;

        //Any object is different than null
        if (!y.HasValue)
            return false;

        if (!x.HasValue)
            return false;

        //Otherwise equals the two values
        return OffsetDateTime.Comparer.Instant.Equals(x.Value, y.Value);
    }

    public int GetHashCode(OffsetDateTime? obj)
    {
        if (obj.HasValue)
            return OffsetDateTime.Comparer.Instant.GetHashCode(obj.Value);
        else
            return 0;
    }


}
