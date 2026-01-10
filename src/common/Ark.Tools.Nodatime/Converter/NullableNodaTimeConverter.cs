// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Nodatime;

/// <summary>
/// Generic base class for TypeConverters that handle nullable NodaTime types.
/// </summary>
/// <typeparam name="T">The underlying value type (e.g., LocalDate, Instant, etc.)</typeparam>
public class NullableNodaTimeConverter<T> : NullableConverter
    where T : struct
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullableNodaTimeConverter{T}"/> class.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The generic type parameter T is known at compile time for each concrete instantiation, making the underlying nullable type T? statically discoverable and trim-safe.")]
    public NullableNodaTimeConverter() : base(typeof(T?))
    {
    }
}
