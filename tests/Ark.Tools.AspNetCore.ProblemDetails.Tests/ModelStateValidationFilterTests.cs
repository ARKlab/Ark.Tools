// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

using System.Reflection;

namespace Ark.Tools.AspNetCore.ProblemDetails.Tests;

/// <summary>Verifies model-state validation filter metadata and outcomes.</summary>
[TestClass]
public sealed class ModelStateValidationFilterTests
{
    [TestMethod]
    public void ReturnsBadRequestForInvalidUnmarkedAction()
    {
        var context = CreateContext(nameof(TestController.UnmarkedAction), isValid: false);

        new Ark.Tools.AspNetCore.ModelStateValidationFilterAttribute().OnActionExecuting(context);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public void DoesNotReturnBadRequestForValidUnmarkedAction()
    {
        var context = CreateContext(nameof(TestController.UnmarkedAction), isValid: true);

        new Ark.Tools.AspNetCore.ModelStateValidationFilterAttribute().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [TestMethod]
    public void SkipsInvalidModelStateForMarkedAction()
    {
        var context = CreateContext(nameof(TestController.MarkedAction), isValid: false);

        new Ark.Tools.AspNetCore.ModelStateValidationFilterAttribute().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [TestMethod]
    public void SkipsValidModelStateForMarkedAction()
    {
        var context = CreateContext(nameof(TestController.MarkedAction), isValid: true);

        new Ark.Tools.AspNetCore.ModelStateValidationFilterAttribute().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [TestMethod]
    public void SupportsDistinctActionMethodsConcurrently()
    {
        var filter = new Ark.Tools.AspNetCore.ModelStateValidationFilterAttribute();

        Parallel.For(0, 100, index =>
        {
            var actionName = index % 2 == 0
                ? nameof(TestController.UnmarkedAction)
                : nameof(TestController.MarkedAction);
            var context = CreateContext(actionName, isValid: false);
            filter.OnActionExecuting(context);
            if (index % 2 == 0)
            {
                context.Result.Should().BeOfType<BadRequestObjectResult>();
            }
            else
            {
                context.Result.Should().BeNull();
            }
        });
    }

    private static ActionExecutingContext CreateContext(string actionName, bool isValid)
    {
        var methodInfo = typeof(TestController).GetMethod(actionName)!;
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new Microsoft.AspNetCore.Routing.RouteData(),
            new ControllerActionDescriptor
            {
                ControllerTypeInfo = typeof(TestController).GetTypeInfo(),
                MethodInfo = methodInfo,
                ActionName = actionName,
            });
        var context = new ActionExecutingContext(
            actionContext,
            Array.Empty<IFilterMetadata>(),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            null!);

        if (!isValid)
        {
            context.ModelState.AddModelError("value", "invalid");
        }

        return context;
    }

    private sealed class TestController : Controller
    {
        public IActionResult UnmarkedAction()
        {
            return Ok();
        }

        [Ark.Tools.AspNetCore.SkipModelStateValidationFilter]
        public IActionResult MarkedAction()
        {
            return Ok();
        }
    }
}
