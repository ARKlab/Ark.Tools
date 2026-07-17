// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Sql.SqlServer;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Ark.Tools.AspNetCore.ProblemDetails;

/// <summary>Maps application exceptions to RFC 7807 responses.</summary>
public static class ExceptionProblemDetailsMapper
{
    /// <summary>Creates a ProblemDetails response for an application exception.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>The mapped response.</returns>
    public static MvcProblemDetails Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            PolicyAuthorizationException => Create(StatusCodes.Status403Forbidden),
            EntityNotFoundException => Create(StatusCodes.Status404NotFound),
            ValidationException validation => CreateValidation(validation),
            EntityTagMismatchException => Create(StatusCodes.Status412PreconditionFailed),
            OptimisticConcurrencyException => Create(StatusCodes.Status409Conflict),
            SqlException sql => Create(SqlExceptionHandler.IsPrimaryKeyOrUniqueKeyViolation(sql)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status500InternalServerError),
            BusinessRuleViolationException businessRule => CreateBusinessRuleViolation(businessRule),
            NotImplementedException => Create(StatusCodes.Status501NotImplemented),
            HttpRequestException => Create(StatusCodes.Status503ServiceUnavailable),
            _ => Create(StatusCodes.Status500InternalServerError),
        };
    }

    private static MvcProblemDetails Create(int statusCode)
    {
        return new MvcProblemDetails
        {
            Status = statusCode,
        };
    }

    private static MvcProblemDetails CreateValidation(ValidationException exception)
    {
        var problemDetails = new MvcProblemDetails
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
    }

    private static MvcProblemDetails CreateBusinessRuleViolation(BusinessRuleViolationException exception)
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

        var problemDetails = new MvcProblemDetails
        {
            Status = violation.Status,
            Title = violation.Title,
            Detail = violation.Detail,
        };
        problemDetails.Extensions["businessRuleViolation"] = payload;
        return problemDetails;
    }
}
