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
    private static readonly ConcurrentDictionary<Type, Accessor[]> _businessRuleViolationAccessors = new();

    /// <summary>Creates a ProblemDetails response for an application exception.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>The mapped response.</returns>
    public static MvcProblemDetails Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            PolicyAuthorizationException => _create(StatusCodes.Status403Forbidden),
            EntityNotFoundException => _create(StatusCodes.Status404NotFound),
            ValidationException validation => _createValidation(validation),
            EntityTagMismatchException => _create(StatusCodes.Status412PreconditionFailed),
            OptimisticConcurrencyException => _create(StatusCodes.Status409Conflict),
            SqlException sql => _create(SqlExceptionHandler.IsPrimaryKeyOrUniqueKeyViolation(sql)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status500InternalServerError),
            BusinessRuleViolationException businessRule => _createBusinessRuleViolation(businessRule),
            NotImplementedException => _create(StatusCodes.Status501NotImplemented),
            HttpRequestException => _create(StatusCodes.Status503ServiceUnavailable),
            _ => _create(StatusCodes.Status500InternalServerError),
        };
    }

    private static MvcProblemDetails _create(int statusCode)
    {
        return new MvcProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Status = statusCode,
        };
    }

    private static MvcProblemDetails _createValidation(ValidationException exception)
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

    private static MvcProblemDetails _createBusinessRuleViolation(BusinessRuleViolationException exception)
    {
        var violation = exception.BusinessRuleViolation;
        var violationType = violation.GetType();
        if (!_businessRuleViolationAccessors.TryGetValue(violationType, out var accessors))
            accessors = _businessRuleViolationAccessors.GetOrAdd(violationType, _createAccessors(violationType));
        var payload = accessors
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

    private static Accessor[] _createAccessors([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type violationType)
    {
        return violationType
            .GetProperties()
            .Where(property => property.GetMethod is not null
                && !property.GetMethod.IsStatic
                && property.GetCustomAttributes<ProblemDetailsExtensionAttribute>(inherit: true).Any())
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(property => property.DeclaringType?.AssemblyQualifiedName, StringComparer.Ordinal)
                .First())
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new Accessor(property.Name, _createGetter(violationType, property)))
            .ToArray();
    }

    private static Func<BusinessRuleViolation, object?> _createGetter(Type violationType, PropertyInfo property)
    {
        var violation = Expression.Parameter(typeof(BusinessRuleViolation), "violation");
        var typedViolation = Expression.Convert(violation, violationType);
        var value = Expression.Property(typedViolation, property);
        var boxedValue = Expression.Convert(value, typeof(object));
        return Expression.Lambda<Func<BusinessRuleViolation, object?>>(boxedValue, violation).Compile();
    }

    private sealed record Accessor(string Name, Func<BusinessRuleViolation, object?> GetValue);
}
