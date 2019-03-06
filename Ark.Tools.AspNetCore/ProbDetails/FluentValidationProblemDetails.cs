using Hellang.Middleware.ProblemDetails;
using System.Collections.Generic;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class FluentValidationProblemDetails : StatusCodeProblemDetails
    {
        public FluentValidationProblemDetails(FluentValidation.ValidationException ex, int statusCode) : base(statusCode)
        {
            var dict = new Dictionary<string, FluentValidationErrors>();

            foreach (var error in ex.Errors)
            {
                string key = error.PropertyName;
                var value = new FluentValidationErrors()
                {
                    AttemptedValue = error.AttemptedValue,
                    CustomState = error.CustomState,
                    ErrorCode = error.ErrorCode,
                    ErrorMessage = error.ErrorMessage,
                    FormattedMessagePlaceholderValues = error.FormattedMessagePlaceholderValues,
                    ResourceName = error.ResourceName,
                };

                dict.Add(key, value);
            }

            Errors = dict;
        }

        public Dictionary<string, FluentValidationErrors> Errors { get; set; }
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
