// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Authorization;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>
/// Configures Hellang ProblemDetails for the sample's transport-agnostic domain exceptions.
/// </summary>
public sealed class SampleProblemDetailsOptionsSetup : IConfigureOptions<Hellang.Middleware.ProblemDetails.ProblemDetailsOptions>
{
    /// <inheritdoc />
    public void Configure(Hellang.Middleware.ProblemDetails.ProblemDetailsOptions options)
    {
        options.Map<PolicyAuthorizationException>(exception => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = exception.Message,
        });
        options.MapToStatusCode<EntityNotFoundException>(StatusCodes.Status404NotFound);
        options.Map<ValidationException>(exception =>
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = exception.Message,
            };
            problemDetails.Extensions["errors"] = exception.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);
            return problemDetails;
        });
        options.Map<BusinessRuleViolationException>(exception =>
        {
            var violation = exception.BusinessRuleViolation;
            var payload = violation.GetType()
                .GetProperties()
                .Where(property => property.DeclaringType != typeof(BusinessRuleViolation))
                .ToDictionary(
                    property => property.Name,
                    property => property.GetValue(violation),
                    StringComparer.Ordinal);
            payload["type"] = violation.GetType().Name;
            payload["title"] = violation.Title;
            payload["status"] = violation.Status;

            var problemDetails = new ProblemDetails
            {
                Status = violation.Status,
                Title = violation.Title,
                Detail = violation.Detail,
            };
            problemDetails.Extensions["businessRuleViolation"] = payload;
            return problemDetails;
        });
    }
}
