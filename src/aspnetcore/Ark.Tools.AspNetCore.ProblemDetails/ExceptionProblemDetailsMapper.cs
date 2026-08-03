// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Sql.SqlServer;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Ark.Tools.AspNetCore.ProblemDetails;

/// <summary>Maps application exceptions to RFC 7807 responses.</summary>
public static class ExceptionProblemDetailsMapper
{
    private static readonly ConcurrentDictionary<Type, Accessor[]> BusinessRuleViolationAccessors = new();

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
            Type = $"https://httpstatuses.com/{statusCode}",
            Status = statusCode,
        };
    }

    private static MvcProblemDetails CreateValidation(ValidationException exception)
    {
        var problemDetails = new MvcProblemDetails
        {
            Type = $"https://httpstatuses.com/{StatusCodes.Status400BadRequest}",
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
        var payload = BusinessRuleViolationAccessors
            .GetOrAdd(violation.GetType(), CreateAccessors)
            .ToDictionary(
                accessor => accessor.Name,
                accessor => accessor.GetValue(violation),
                StringComparer.Ordinal);
        payload["type"] = violation.GetType().Name;
        payload["title"] = violation.Title;
        payload["status"] = violation.Status;

        var problemDetails = new MvcProblemDetails
        {
            Type = $"https://httpstatuses.com/{violation.Status}",
            Status = violation.Status,
            Title = violation.Title,
            Detail = violation.Detail,
        };
        problemDetails.Extensions["businessRuleViolation"] = payload;
        return problemDetails;
    }

    private static Accessor[] CreateAccessors(Type violationType)
    {
        return violationType
            .GetProperties()
            .Where(property => property.DeclaringType != typeof(BusinessRuleViolation))
            .Select(property => new Accessor(property.Name, CreateGetter(violationType, property)))
            .ToArray();
    }

    private static Func<BusinessRuleViolation, object?> CreateGetter(Type violationType, PropertyInfo property)
    {
        var violation = Expression.Parameter(typeof(BusinessRuleViolation), "violation");
        var typedViolation = Expression.Convert(violation, violationType);
        var value = Expression.Property(typedViolation, property);
        var boxedValue = Expression.Convert(value, typeof(object));
        return Expression.Lambda<Func<BusinessRuleViolation, object?>>(boxedValue, violation).Compile();
    }

    private sealed record Accessor(string Name, Func<BusinessRuleViolation, object?> GetValue);
}
