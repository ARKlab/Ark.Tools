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
    }
}
