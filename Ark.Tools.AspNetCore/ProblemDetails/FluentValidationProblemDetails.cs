using Hellang.Middleware.ProblemDetails;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class FluentValidationProblemDetails : StatusCodeProblemDetails
    {
        public FluentValidationProblemDetails(FluentValidation.ValidationException ex, int statusCode) : base(statusCode)
        {
            Errors = ex.Errors?.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error =>
                    new FluentValidationErrors()
                    {
                        AttemptedValue = error.AttemptedValue,
                        CustomState = error.CustomState,
                        ErrorCode = error.ErrorCode,
                        ErrorMessage = error.ErrorMessage,
                        FormattedMessagePlaceholderValues = error.FormattedMessagePlaceholderValues,
                        ResourceName = error.ResourceName,
                    }
                ).ToArray());

			Detail = string.Join(Environment.NewLine, Errors?.SelectMany(s => s.Value.Select(x => x.ErrorMessage)));
		}

        public Dictionary<string, FluentValidationErrors[]> Errors { get; set; }
    }

    public class FluentValidationErrors
    {
        //
        // Summary:
        //     The error message
        public string ErrorMessage { get; set; }
        //
        // Summary:
        //     The property value that caused the failure.
        public object AttemptedValue { get; set; }
        //
        // Summary:
        //     Custom state associated with the failure.
        public object CustomState { get; set; }
        //
        // Summary:
        //     Gets or sets the error code.
        public string ErrorCode { get; set; }
        //
        // Summary:
        //     Gets or sets the formatted message placeholder values.
        public Dictionary<string, object> FormattedMessagePlaceholderValues { get; set; }
        //
        // Summary:
        //     The resource name used for building the message
        public string ResourceName { get; set; }
    }
}
