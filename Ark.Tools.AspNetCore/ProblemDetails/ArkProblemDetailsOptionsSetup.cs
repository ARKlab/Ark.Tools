using System;
using System.Data;
using System.Net.Http;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore
{
    public class ArkProblemDetailsOptionsSetup : IConfigureOptions<ProblemDetailsOptions>
    {
        public ArkProblemDetailsOptionsSetup(IHostingEnvironment environment,
            IHttpContextAccessor httpContextAccessor, IOptions<ApiBehaviorOptions> apiOptions)
        {
            Environment = environment;
            HttpContextAccessor = httpContextAccessor;
            ApiOptions = apiOptions.Value;
        }

        private IHostingEnvironment Environment { get; }
        private IHttpContextAccessor HttpContextAccessor { get; }
        private ApiBehaviorOptions ApiOptions { get; }

        public void Configure(ProblemDetailsOptions options)
        {
            options.IncludeExceptionDetails = ctx => Environment.IsDevelopment();

            options.MapStatusCode = _mapStatusCode;

            options.OnBeforeWriteDetails = (ctx, details) =>
            {
                // keep consistent with asp.net core 2.2 conventions that adds a tracing value
                ProblemDetailsHelper.SetTraceId(details, HttpContextAccessor.HttpContext);
            };

            _configureProblemDetails(options);
        }

        private void _configureProblemDetails(ProblemDetailsOptions options)
        {
            // This is the default behavior; only include exception details in a development environment.
            options.IncludeExceptionDetails = ctx => Environment.IsDevelopment(); 

            // This will map DBConcurrencyException to the 409 Conflict status code.
            options.Map<DBConcurrencyException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status409Conflict));

            // This will map NotImplementedException to the 501 Not Implemented status code.
            options.Map<NotImplementedException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status501NotImplemented));

            // This will map HttpRequestException to the 503 Service Unavailable status code.
            options.Map<HttpRequestException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status503ServiceUnavailable));

            // Because exceptions are handled polymorphically, this will act as a "catch all" mapping, which is why it's added last.
            // If an exception other than NotImplementedException and HttpRequestException is thrown, this will handle it.
            options.Map<Exception>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status500InternalServerError));
        }

        private ProblemDetails _mapStatusCode(HttpContext context, int statusCode)
        {
            if (!ApiOptions.SuppressMapClientErrors &&
                ApiOptions.ClientErrorMapping.TryGetValue(statusCode, out var errorData))
            {
                // prefer the built-in mapping in asp.net core
                return new ProblemDetails
                {
                    Status = statusCode,
                    Title = errorData.Title,
                    Type = errorData.Link
                };
            }
            else
            {
                // use Hellang.Middleware.ProblemDetails mapping
                return new StatusCodeProblemDetails(statusCode);
            }
        }
    }
}