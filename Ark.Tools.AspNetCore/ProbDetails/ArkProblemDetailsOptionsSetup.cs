using System;
using System.Data;
using System.Net.Http;
using Ark.Tools.Core;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class ArkProblemDetailsOptionsSetup : IConfigureOptions<ProblemDetailsOptions>
    {
        public ArkProblemDetailsOptionsSetup(IHostingEnvironment environment, IProblemDetailsLinkGenerator linkGenerator,
            IProblemDetailsRouterProvider problemDetailsRouter)
        {
            Environment = environment;
            LinkGenerator = linkGenerator;
            _problemDetailsRouter = problemDetailsRouter;
        }

        private IHostingEnvironment Environment { get; }
        private IProblemDetailsLinkGenerator LinkGenerator { get; }

        //private readonly IEndpointAddressScheme<RouteValuesAddress> _endpointAddress;
        private readonly IProblemDetailsRouterProvider _problemDetailsRouter;

        //private ApiBehaviorOptions ApiOptions { get; }

        public void Configure(ProblemDetailsOptions options)
        {
            // This is the default behavior; only include exception details in a development environment.
            //options.IncludeExceptionDetails = ctx => Environment.IsDevelopment();

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

            _configureProblemDetails(options);
        }

        private void _configureProblemDetails(ProblemDetailsOptions options)
        {
            // This will map DBConcurrencyException to the 409 Conflict status code.
            options.Map<EntityNotFoundException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status404NotFound));

            // This will map NotImplementedException to the 501 Not Implemented status code.
            options.Map<NotImplementedException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status501NotImplemented));

            // This will map HttpRequestException to the 503 Service Unavailable status code.
            options.Map<HttpRequestException>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status503ServiceUnavailable));

            // Because exceptions are handled polymorphically, this will act as a "catch all" mapping, which is why it's added last.
            // If an exception other than NotImplementedException and HttpRequestException is thrown, this will handle it.
            options.Map<Exception>(ex => new ExceptionProblemDetails(ex, StatusCodes.Status500InternalServerError));
        }

        private static bool IsServerError(int? statusCode)
        {
            // Err on the side of caution and treat missing status code as server error.
            return !statusCode.HasValue || statusCode.Value >= 500;
        }

        //private ProblemDetails _mapStatusCode(HttpContext context, int statusCode)
        //{
        //    if (!ApiOptions.SuppressMapClientErrors &&
        //        ApiOptions.ClientErrorMapping.TryGetValue(statusCode, out var errorData))
        //    {
        //        // prefer the built-in mapping in asp.net core
        //        return new ProblemDetails
        //        {
        //            Status = statusCode,
        //            Title = errorData.Title,
        //            Type = $"https://httpstatuses.com/{statusCode}" //errorData.Link
        //        };
        //    }
        //    else
        //    {
        //        // use Hellang.Middleware.ProblemDetails mapping
        //        return new StatusCodeProblemDetails(statusCode);
        //    }
        //}

        private static bool IsProblem(HttpContext context)
        {
            if (context.Response.StatusCode < 400)
            {
                return false;
            }

            if (context.Response.StatusCode >= 600)
            {
                return false;
            }

            if (context.Response.ContentLength.HasValue)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.Response.ContentType))
            {
                return true;
            }

            return false;
        }
    }
}