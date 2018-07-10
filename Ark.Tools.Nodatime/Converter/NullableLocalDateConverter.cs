// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime
{
    public class NullableLocalDateConverter : NullableConverter
    {
        public NullableLocalDateConverter() : base(typeof(LocalDate?)) { }
    }
}
