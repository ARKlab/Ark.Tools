// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Nodatime;

public class NullableOffsetDateTimeConverter : NullableConverter
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The underlying type OffsetDateTime? is known at compile time and will not be trimmed.")]
    public NullableOffsetDateTimeConverter() : base(typeof(OffsetDateTime?)) { }
}