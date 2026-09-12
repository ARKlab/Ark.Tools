// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

namespace Ark.Tools.Core.Interceptors.Tests;

/// <summary>
/// A hand-written sensitive value object (same contract the Ark.Tools.Compliance generator emits)
/// used to prove that <c>ToDataTableArk()</c> converts such members to their cleartext transport
/// string - via the inventoried Reveal egress - in both the interceptor and reflection paths.
/// </summary>
public readonly struct InterceptedSensitiveValue : ISensitiveValue<InterceptedSensitiveValue>, IEquatable<InterceptedSensitiveValue>
{
    private readonly string _value;

    private InterceptedSensitiveValue(string value)
    {
        _value = value;
    }

    /// <inheritdoc cref="ISensitiveValue{TSelf}.From"/>
    public static InterceptedSensitiveValue From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new InterceptedSensitiveValue(value);
    }

    /// <inheritdoc cref="ISensitiveValue{TSelf}.TryFrom"/>
    public static bool TryFrom(string? value, out InterceptedSensitiveValue result)
    {
        if (value is null)
        {
            result = default;
            return false;
        }

        result = new InterceptedSensitiveValue(value);
        return true;
    }

    /// <inheritdoc cref="ISensitiveValue{TSelf}.Reveal"/>
    public string Reveal(CompliancePurpose purpose, CompliancePurposeCategory category)
    {
        if (string.IsNullOrWhiteSpace(purpose.Reason))
            throw new ArgumentException("A compliance purpose is required.", nameof(purpose));
        if (category == CompliancePurposeCategory.Unspecified || !Enum.IsDefined(category))
            throw new ArgumentException("A defined compliance purpose category is required.", nameof(category));
        return _value ?? string.Empty;
    }

    /// <inheritdoc />
    public override string ToString() => "***";

    /// <inheritdoc />
    public bool Equals(InterceptedSensitiveValue other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    [SuppressMessage("Compliance", "ARKPII010", Justification = "Standard Equals(object) override; the value renders redacted and only Reveal yields cleartext.")]
    public override bool Equals(object? obj) => obj is InterceptedSensitiveValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(InterceptedSensitiveValue left, InterceptedSensitiveValue right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(InterceptedSensitiveValue left, InterceptedSensitiveValue right) => !left.Equals(right);
}

/// <summary>A flat entity carrying required and optional sensitive value members.</summary>
public sealed class InterceptedSensitiveEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the owner.</summary>
    public InterceptedSensitiveValue Owner { get; set; }

    /// <summary>Gets or sets the optional co-owner.</summary>
    public InterceptedSensitiveValue? CoOwner { get; set; }
}
