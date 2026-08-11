// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;

namespace Ark.Tools.Core.Interceptors.Tests;

/// <summary>Status used by <see cref="InterceptedEntity.Status"/> to exercise the enum-to-string interceptor conversion.</summary>
public enum InterceptedStatus
{
    /// <summary>Pending status.</summary>
    Pending = 0,

    /// <summary>Active status.</summary>
    Active = 1,
}

/// <summary>
/// A "flat" (no custom base type), globally-accessible, 10-mixed-type-property POCO used to prove
/// that <c>ToDataTableArk()</c> calls with this compile-time-known type are intercepted by
/// <c>ToDataTableArkInterceptorGenerator</c> rather than falling back to reflection.
/// </summary>
public sealed class InterceptedEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a floating point measurement.</summary>
    public double Measurement { get; set; }

    /// <summary>Gets or sets a monetary amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets a flag.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets an optional counter.</summary>
    public int? OptionalCount { get; set; }

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public InterceptedStatus Status { get; set; }

    /// <summary>Gets or sets a unique identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets a NodaTime local date.</summary>
    public LocalDate EffectiveDate { get; set; }
}

/// <summary>A public struct T, to prove the interceptor also handles non-primitive value types.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public record struct InterceptedPoint
{
    /// <summary>Gets or sets the X coordinate.</summary>
    public int X { get; set; }

    /// <summary>Gets or sets the Y coordinate.</summary>
    public int Y { get; set; }
}

/// <summary>A public type with mixed members used to verify fields-before-properties ordering.</summary>
public sealed class MixedMemberEntity
{
    /// <summary>Gets or sets the property value.</summary>
    public int Property { get; set; }

    /// <summary>The field value.</summary>
    [SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Required to verify DataTable field ordering.")]
    public int Field;
}

/// <summary>A public type with a static member that must use the reflection fallback.</summary>
public sealed class StaticMemberEntity
{
    /// <summary>The constant value excluded by the reflection fallback.</summary>
    public const int Constant = 7;

    /// <summary>Gets or sets the instance value.</summary>
    public int Value { get; set; }
}

/// <summary>
/// A type deriving from a custom base class, deliberately ineligible for interception (the
/// generator only intercepts "flat" classes deriving directly from <see cref="object"/>), used to
/// prove such calls safely fall back to the reflection-based implementation.
/// </summary>
public class InterceptedEntityBase
{
    /// <summary>Gets or sets the base identifier.</summary>
    public int BaseId { get; set; }
}

/// <inheritdoc cref="InterceptedEntityBase"/>
public sealed class InterceptedEntityDerived : InterceptedEntityBase
{
    /// <summary>Gets or sets the derived name.</summary>
    public string DerivedName { get; set; } = string.Empty;
}

/// <summary>Helper exposing a call site where T is an open generic type parameter, which can never be intercepted.</summary>
public static class GenericFallbackHelper
{
    /// <summary>
    /// Converts a sequence to a DataTable through a generic method. Because <typeparamref name="T"/>
    /// is an open type parameter at this call site (regardless of what concrete type callers use),
    /// the interceptor generator can never target this specific invocation; it always executes the
    /// reflection-based fallback in <c>ShredObjectToDataTable&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>The resulting DataTable.</returns>
    public static System.Data.DataTable ConvertGeneric<T>(IEnumerable<T> source) => source.ToDataTableArk();
}
