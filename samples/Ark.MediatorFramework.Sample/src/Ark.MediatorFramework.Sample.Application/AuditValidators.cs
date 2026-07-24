// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using FluentValidation;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Validates audit query paging parameters.</summary>
public sealed class GetAuditsValidator : AbstractValidator<GetAuditsQuery>
{
    /// <summary>Initializes a new instance of the <see cref="GetAuditsValidator"/> class.</summary>
    public GetAuditsValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);

        When(query => query.Sort is not null, () =>
        {
            RuleForEach(query => query.Sort)
                .Must(_isValidSort)
                .WithMessage("Invalid audit sort '{PropertyValue}'.");
        });
    }

    private static readonly HashSet<string> _sortProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AuditRecord.Id),
        nameof(AuditRecord.UserId),
        nameof(AuditRecord.EntityType),
        nameof(AuditRecord.Identifier),
        nameof(AuditRecord.Operation),
        nameof(AuditRecord.Timestamp),
    };

    private static bool _isValidSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return true;

        var parts = sort.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2)
            return false;

        if (!_sortProperties.Contains(parts[0]))
            return false;

        return parts.Length == 1
            || parts[1].Equals("ASC", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase);
    }
    }
