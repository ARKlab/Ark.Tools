// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>
/// Names the reviewed purpose for an explicit clear-text data reveal.
/// </summary>
public readonly struct CompliancePurpose : IEquatable<CompliancePurpose>
{
    private readonly string _reason;

    private CompliancePurpose(string reason)
    {
        _reason = reason;
    }

    /// <summary>
    /// Gets the purpose for sending a transactional email.
    /// </summary>
    public static CompliancePurpose SendTransactionalEmail => new("SendTransactionalEmail");

    /// <summary>
    /// Creates a purpose with an explicitly recorded reason.
    /// </summary>
    /// <param name="reason">The reason for revealing clear-text data.</param>
    /// <returns>A custom compliance purpose.</returns>
    public static CompliancePurpose Custom(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new CompliancePurpose(reason);
    }

    /// <summary>
    /// Gets the recorded purpose reason.
    /// </summary>
    public string Reason => _reason;

    /// <summary>
    /// Gets the recorded purpose reason.
    /// </summary>
    public string Value => _reason;

    /// <inheritdoc />
    public bool Equals(CompliancePurpose other)
    {
        return string.Equals(_reason, other._reason, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is CompliancePurpose other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _reason?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _reason ?? string.Empty;
    }

    /// <summary>
    /// Compares two compliance purposes.
    /// </summary>
    public static bool operator ==(CompliancePurpose left, CompliancePurpose right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two compliance purposes.
    /// </summary>
    public static bool operator !=(CompliancePurpose left, CompliancePurpose right)
    {
        return !left.Equals(right);
    }
}
