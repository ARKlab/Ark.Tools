// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Hellang.Middleware.ProblemDetails;


namespace Ark.Tools.AspNetCore.ProblemDetails;

public class FluentValidationProblemDetails : StatusCodeProblemDetails
{
    public FluentValidationProblemDetails(FluentValidation.ValidationException ex, int statusCode) : base(statusCode)
    {
        Errors = ex.Errors?.GroupBy(static x => x.PropertyName, StringComparer.Ordinal)
            .ToDictionary(static x => x.Key, static x => x.Select(static error =>
                new FluentValidationErrors()
                {
                    AttemptedValue = error.AttemptedValue,
                    CustomState = error.CustomState,
                    ErrorCode = error.ErrorCode,
                    ErrorMessage = error.ErrorMessage,
                    FormattedMessagePlaceholderValues = error.FormattedMessagePlaceholderValues,
                }
            ).ToArray(), StringComparer.Ordinal) ?? new Dictionary<string, FluentValidationErrors[]>(StringComparer.Ordinal);

        Detail = string.Join(Environment.NewLine, Errors.SelectMany(static s => s.Value.Select(static x => x.ErrorMessage)));
    }

    public Dictionary<string, FluentValidationErrors[]> Errors { get; set; }
}

public record FluentValidationErrors
{
    public string? ErrorMessage { get; init; }
    public object? AttemptedValue { get; init; }
    public object? CustomState { get; init; }
    public string? ErrorCode { get; init; }
    public Dictionary<string, object> FormattedMessagePlaceholderValues { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
}