// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System.ComponentModel;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Nodatime(net10.0)', Before:
namespace Ark.Tools.Nodatime
{
    public class NullableOffsetDateTimeConverter : NullableConverter
    {
        public NullableOffsetDateTimeConverter() : base(typeof(OffsetDateTime?)) { }
    }
=======
namespace Ark.Tools.Nodatime;

public class NullableOffsetDateTimeConverter : NullableConverter
{
    public NullableOffsetDateTimeConverter() : base(typeof(OffsetDateTime?)) { }
>>>>>>> After


namespace Ark.Tools.Nodatime;

public class NullableOffsetDateTimeConverter : NullableConverter
{
    public NullableOffsetDateTimeConverter() : base(typeof(OffsetDateTime?)) { }
}