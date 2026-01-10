// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Nodatime;

public class NullableLocalDateTimeConverter : NullableConverter
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The underlying type LocalDateTime? is known at compile time and will not be trimmed.")]
    public NullableLocalDateTimeConverter() : base(typeof(LocalDateTime?)) { }
}