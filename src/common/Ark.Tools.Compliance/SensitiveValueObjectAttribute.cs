// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>Controls which generated serializers are emitted for a sensitive value object.</summary>
[Flags]
public enum SerializationTargets
{
    /// <summary>Do not emit serialization support.</summary>
    None = 0,

    /// <summary>Emit a System.Text.Json converter.</summary>
    SystemTextJson = 1,

    /// <summary>Emit a Dapper type handler.</summary>
    Dapper = 2,

    /// <summary>Emit all supported serializers.</summary>
    All = SystemTextJson | Dapper,
}

/// <summary>Marks a readonly partial string value object for safe generated rendering.</summary>
/// <typeparam name="T">The underlying value type; only <see cref="string"/> is supported.</typeparam>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SensitiveValueObjectAttribute<T> : Attribute
{
    /// <summary>Initializes the attribute.</summary>
    /// <param name="redaction">The default redaction mode.</param>
    /// <param name="serialization">The serializers to generate.</param>
    public SensitiveValueObjectAttribute(
        ArkRedaction redaction = ArkRedaction.Erase,
        SerializationTargets serialization = SerializationTargets.All)
    {
        Redaction = redaction;
        Serialization = serialization;
    }

    /// <summary>Gets the default redaction mode.</summary>
    public ArkRedaction Redaction { get; }

    /// <summary>Gets the serializers to generate.</summary>
    public SerializationTargets Serialization { get; }
}

/// <summary>Represents the result of validating a sensitive value object.</summary>
public readonly struct ValidationResult : IEquatable<ValidationResult>
{
    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets a successful validation result.</summary>
    public static ValidationResult Ok => new(true, null);

    /// <summary>Creates a failed validation result.</summary>
    /// <param name="errorMessage">A safe error message that does not contain the input value.</param>
    public static ValidationResult Invalid(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new ValidationResult(false, errorMessage);
    }

    /// <summary>Gets whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the safe validation error, when validation failed.</summary>
    public string? ErrorMessage { get; }

    /// <inheritdoc />
    public bool Equals(ValidationResult other)
    {
        return IsValid == other.IsValid
            && string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ValidationResult other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(IsValid, ErrorMessage);
    }

    /// <summary>Compares validation results.</summary>
    public static bool operator ==(ValidationResult left, ValidationResult right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares validation results.</summary>
    public static bool operator !=(ValidationResult left, ValidationResult right)
    {
        return !left.Equals(right);
    }
}
