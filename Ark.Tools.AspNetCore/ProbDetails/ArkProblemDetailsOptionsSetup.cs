using System;
using System.Data.SqlClient;
using System.Net.Http;
using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Sql.SqlServer;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ArkProblemDetailsOptionsSetup : IConfigureOptions<ProblemDetailsOptions>
    {
        public ArkProblemDetailsOptionsSetup(IHostingEnvironment environment, IProblemDetailsLinkGenerator linkGenerator)
        {
            Environment = environment;
            LinkGenerator = linkGenerator;
        }

        private IHostingEnvironment Environment { get; }
        private IProblemDetailsLinkGenerator LinkGenerator { get; }

        public void Configure(ProblemDetailsOptions options)
        {
            // This is the default behavior; only include exception details in a development environment.
            options.IncludeExceptionDetails = ctx => Environment.IsDevelopment();

            options.ShouldLogUnhandledException = (ctx, e, d) => IsServerError(d.Status);

            options.MapStatusCode = (ctx, statusCode) => new StatusCodeProblemDetails(statusCode);

            options.IsProblem = IsProblem;

            options.OnBeforeWriteDetails = (ctx, details) =>
            {
                // keep consistent with asp.net core 2.2 conventions that adds a tracing value
                ProblemDetailsHelper.SetTraceId(details, ctx);

                if (details is ArkProblemDetails)
                {
                    var path = LinkGenerator.GetLink(details as ArkProblemDetails, ctx);
                    details.Type = details.Type ?? path;
                }  
            };

            _configureExceptionProblemDetails(options);
        }

        // This will map Exceptions to the corresponding Conflict status code.
        private void _configureExceptionProblemDetails(ProblemDetailsOptions options)
        {
            options.Map<EntityNotFoundException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status404NotFound));

            options.Map<NotImplementedException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status501NotImplemented));

            options.Map<HttpRequestException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status503ServiceUnavailable));

            options.Map<UnauthorizedAccessException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status403Forbidden));

            options.Map<EntityTagMismatchException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status412PreconditionFailed));

            options.Map<OptimisticConcurrencyException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status409Conflict));

            options.Map<SqlException>(ex => SqlExceptionHandler.IsPrimaryKeyOrUniqueKeyViolation(ex) 
                ? new ExceptionProblemDetails(ex, StatusCodes.Status409Conflict)
                : new ExceptionProblemDetails(ex, StatusCodes.Status500InternalServerError));

            options.Map<FluentValidation.ValidationException>(ex => new FluentValidationProblemDetails(ex, StatusCodes.Status400BadRequest));

            // If an exception other than above specified is thrown, this will handle it.
            options.Map<Exception>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status500InternalServerError));
        }

        private static bool IsServerError(int? statusCode)
        {
            // Err on the side of caution and treat missing status code as server error.
            return !statusCode.HasValue || statusCode.Value >= 500;
        }

        private static bool IsProblem(HttpContext context)
        {
            if (context.Response.StatusCode < 400)
                return false;

            if (context.Response.StatusCode >= 600)
                return false;

            if (context.Response.ContentLength.HasValue)
                return false;

            if (string.IsNullOrEmpty(context.Response.ContentType))
                return true;

            return false;
        }
    }
}