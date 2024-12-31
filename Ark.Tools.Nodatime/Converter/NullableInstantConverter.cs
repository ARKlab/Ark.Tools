﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public class NullableInstantConverter : NullableConverter
    {
        public NullableInstantConverter() : base(typeof(Instant?)) { }
    }
}
