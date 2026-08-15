// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core;

/// <summary>
/// Thrown when an <see cref="EvolvableEnum{TEnum}"/> value cannot be represented on the requested
/// transport or wire format because the required mapping (a symbolic name or a numeric value) is
/// missing. This is a deliberate, explicit failure: an evolvable enum never silently substitutes a
/// default or corrupts data when it cannot honor the requested representation.
/// </summary>
[SuppressMessage("Design", "MA0049:Type name should not match namespace", Justification = "The exception name intentionally mirrors the value type it protects.")]
public sealed class EvolvableEnumConversionException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="EvolvableEnumConversionException"/> class.</summary>
    public EvolvableEnumConversionException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EvolvableEnumConversionException"/> class with the given message.</summary>
    /// <param name="message">The error message.</param>
    public EvolvableEnumConversionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EvolvableEnumConversionException"/> class with the given message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public EvolvableEnumConversionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
