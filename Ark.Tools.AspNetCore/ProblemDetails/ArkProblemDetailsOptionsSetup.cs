using System;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Sql.SqlServer;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Ark.Tools.Core.BusinessRuleViolation;
using System.Diagnostics;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ArkProblemDetailsOptionsSetup 
        : IConfigureOptions<ProblemDetailsOptions>
        , IPostConfigureOptions<ProblemDetailsOptions>
    {
        public ArkProblemDetailsOptionsSetup(IWebHostEnvironment environment, IProblemDetailsLinkGenerator linkGenerator)
        {
            Environment = environment;
            LinkGenerator = linkGenerator;
        }

        private IWebHostEnvironment Environment { get; }
        private IProblemDetailsLinkGenerator LinkGenerator { get; }

        public void Configure(ProblemDetailsOptions options)
        {
            // This is the default behavior; only include exception details in a development environment.
            options.IncludeExceptionDetails = (ctx, ex) => true; // !Environment.IsProduction();

            options.ShouldLogUnhandledException = (ctx, e, d) => _isServerError(d.Status);

            options.IsProblem = _isProblem;

            // keep consistent with asp.net core 2.2 conventions that adds a tracing value
            options.GetTraceId = ctx => Activity.Current?.Id ?? ctx.TraceIdentifier;

            options.OnBeforeWriteDetails = (ctx, details) =>
            {
                if ( Environment.IsProduction() && (details.Status >= 400 && details.Status < 500))
                {
                    if (details.Extensions.ContainsKey(options.ExceptionDetailsPropertyName))
                    {
                        details.Extensions.Remove(options.ExceptionDetailsPropertyName);
                    }
                }

                if (details is ArkProblemDetails)
                {
                    var path = LinkGenerator.GetLink(details as ArkProblemDetails, ctx);
                    details.Type = details.Type ?? path;
                }

                if (details is BusinessRuleProblemDetails br)
                {
                    var path = LinkGenerator.GetLink(br.Violation, ctx);
                    details.Type = path;
                }
            };

            _configureExceptionProblemDetails(options);
        }

        // This will map Exceptions to the corresponding Conflict status code.
        private void _configureExceptionProblemDetails(ProblemDetailsOptions options)
        {

            options.MapToStatusCode<EntityNotFoundException>(StatusCodes.Status404NotFound);

            options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);

            options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);

            options.MapToStatusCode<UnauthorizedAccessException>(StatusCodes.Status403Forbidden);

            options.MapToStatusCode<EntityTagMismatchException>(StatusCodes.Status412PreconditionFailed);

            options.MapToStatusCode<OptimisticConcurrencyException>(StatusCodes.Status409Conflict);

            options.Map<SqlException>(ex => SqlExceptionHandler.IsPrimaryKeyOrUniqueKeyViolation(ex) 
                ? StatusCodeProblemDetails.Create(StatusCodes.Status409Conflict)
                : StatusCodeProblemDetails.Create(StatusCodes.Status500InternalServerError));

            options.Map<FluentValidation.ValidationException>(ex => new FluentValidationProblemDetails(ex, StatusCodes.Status400BadRequest));

            options.Map<BusinessRuleViolationException>(ex => new BusinessRuleProblemDetails(ex.BusinessRuleViolation));
        }

        private static bool _isServerError(int? statusCode)
        {
            // Err on the side of caution and treat missing status code as server error.
            return !statusCode.HasValue || statusCode.Value >= 500;
        }

        private static bool _isProblem(HttpContext context)
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

        public void PostConfigure(string name, ProblemDetailsOptions options)
        {
            // If an exception other than above specified is thrown, this will handle it.
            options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
        }
    }
}