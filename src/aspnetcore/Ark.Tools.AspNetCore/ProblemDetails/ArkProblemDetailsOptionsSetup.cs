// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Core.Reflection;
using Ark.Tools.Sql.SqlServer;

using Hellang.Middleware.ProblemDetails;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

using ProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ArkProblemDetailsOptionsSetup
        : IConfigureOptions<ProblemDetailsOptions>
        , IPostConfigureOptions<ProblemDetailsOptions>
    {
        public ArkProblemDetailsOptionsSetup(IWebHostEnvironment environment, IProblemDetailsLinkGenerator linkGenerator)
        {
            _environment = environment;
            _linkGenerator = linkGenerator;
            _dynamicTypeAssembly = new DynamicTypeAssembly();
            _brvMap = new ConcurrentDictionary<Type, Type>();
        }

        private readonly IWebHostEnvironment _environment;
        private readonly IProblemDetailsLinkGenerator _linkGenerator;
        private readonly DynamicTypeAssembly _dynamicTypeAssembly;
        private readonly ConcurrentDictionary<Type, Type> _brvMap;

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
                if (_environment.IsProduction() && (details.Status >= 400 && details.Status < 500))
                {
                    if (details.Extensions.ContainsKey(options.ExceptionDetailsPropertyName))
                    {
                        details.Extensions.Remove(options.ExceptionDetailsPropertyName);
                    }
                }

                if (details is ArkProblemDetails apd)
                {
                    var path = _linkGenerator.GetLink(apd, ctx);
                    details.Type ??= path;
                }

                if (details.Extensions.TryGetValue("@BusinessRuleViolation", out var v))
                {
                    details.Extensions.Remove("@BusinessRuleViolation");
                    if (v is BusinessRuleViolation brv)
                    {
                        var path = _linkGenerator.GetLink(brv, ctx);
                        details.Type ??= path;
                    }
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

            options.Map<BusinessRuleViolationException>(_toProblemDetails);
        }

        private Microsoft.AspNetCore.Mvc.ProblemDetails _toProblemDetails(BusinessRuleViolationException arg)
        {
            var pdt = _brvMap.GetOrAdd(arg.BusinessRuleViolation.GetType(), t =>
            {
                var props = t.GetProperties().Where(x => x.DeclaringType != typeof(BusinessRuleViolation)).Select(x => (x.Name, x.PropertyType)).ToArray();
                return _dynamicTypeAssembly.CreateNewTypeWithDynamicProperties(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), props);
            });

            var js = (arg.BusinessRuleViolation as object).SerializeToByte(ArkSerializerOptions.JsonOptions);
            var ret = (Microsoft.AspNetCore.Mvc.ProblemDetails)JsonSerializer.Deserialize(js, pdt, ArkSerializerOptions.JsonOptions)!;
            ret.Extensions["@BusinessRuleViolation"] = arg.BusinessRuleViolation;
            return ret;
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

        public void PostConfigure(string? name, ProblemDetailsOptions options)
        {
            // If an exception other than above specified is thrown, this will handle it.
            options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
        }
    }
}