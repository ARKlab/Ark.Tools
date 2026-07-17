// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using FluentValidation;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Validates requests that create greetings.</summary>
public sealed class CreateGreetingValidator : AbstractValidator<CreateGreetingRequest>
{
    /// <summary>Initializes a new instance of the <see cref="CreateGreetingValidator"/> class.</summary>
    public CreateGreetingValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Name must not be empty.");
    }
}
