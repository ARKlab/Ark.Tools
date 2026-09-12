// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>
/// Names the reviewed purpose for an explicit clear-text data reveal, together with its
/// GDPR-style processing category recorded in the compliance inventory.
/// </summary>
public readonly struct CompliancePurpose : IEquatable<CompliancePurpose>
{
    private readonly string? _reason;
    private readonly CompliancePurposeCategory _category;

    private CompliancePurpose(string reason, CompliancePurposeCategory category)
    {
        _reason = reason;
        _category = category;
    }

    /// <summary>
    /// Gets the purpose for sending a transactional email.
    /// </summary>
    public static CompliancePurpose SendTransactionalEmail => new("SendTransactionalEmail", CompliancePurposeCategory.CustomerSupport);

    /// <summary>
    /// Creates a purpose with an explicitly recorded reason and processing category.
    /// </summary>
    /// <param name="reason">The reason for revealing clear-text data.</param>
    /// <param name="category">The GDPR-style category of the processing this reveal serves.</param>
    /// <returns>A custom compliance purpose.</returns>
    public static CompliancePurpose Custom(string reason, CompliancePurposeCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (category == CompliancePurposeCategory.Unspecified || !Enum.IsDefined(category))
            throw new ArgumentException("A defined compliance purpose category is required.", nameof(category));
        return new CompliancePurpose(reason, category);
    }

    /// <summary>
    /// Gets the recorded purpose reason.
    /// </summary>
    public string Reason => _reason ?? string.Empty;

    /// <summary>
    /// Gets the recorded purpose reason.
    /// </summary>
    public string Value => _reason ?? string.Empty;

    /// <summary>
    /// Gets the GDPR-style category of the processing this purpose serves.
    /// </summary>
    public CompliancePurposeCategory Category => _category;

    /// <inheritdoc />
    public bool Equals(CompliancePurpose other)
    {
        return string.Equals(_reason, other._reason, StringComparison.Ordinal) && _category == other._category;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is CompliancePurpose other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(_reason?.GetHashCode(StringComparison.Ordinal) ?? 0, _category);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _reason is null ? string.Empty : _reason + " [" + _category + "]";
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
