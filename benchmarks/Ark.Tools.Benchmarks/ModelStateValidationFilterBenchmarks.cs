// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using BenchmarkDotNet.Attributes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

using System.Reflection;

namespace Ark.Tools.Benchmarks;

[Config(typeof(ExceptionProblemDetailsBenchmarks.BenchmarkConfig))]
public class ModelStateValidationFilterBenchmarks
{
    private readonly Ark.Tools.AspNetCore.ModelStateValidationFilterAttribute _filter = new();
    private ActionExecutingContext _markedContext = null!;
    private ActionExecutingContext _unmarkedContext = null!;
    private MethodInfo _markedMethod = null!;
    private MethodInfo _unmarkedMethod = null!;

    [GlobalSetup]
    public void Setup()
    {
        _markedMethod = typeof(TestController).GetMethod(nameof(TestController.MarkedAction))!;
        _unmarkedMethod = typeof(TestController).GetMethod(nameof(TestController.UnmarkedAction))!;
        _markedContext = CreateContext(_markedMethod);
        _unmarkedContext = CreateContext(_unmarkedMethod);
        _filter.OnActionExecuting(_markedContext);
        _filter.OnActionExecuting(_unmarkedContext);
    }

    [Benchmark(Baseline = true)]
    public bool ReflectionMarked()
    {
        return HasSkipAttribute(_markedMethod);
    }

    [Benchmark]
    public bool CachedMarked()
    {
        _filter.OnActionExecuting(_markedContext);
        return _markedContext.Result is null;
    }

    [Benchmark]
    public bool ReflectionUnmarked()
    {
        return HasSkipAttribute(_unmarkedMethod);
    }

    [Benchmark]
    public bool CachedUnmarked()
    {
        _filter.OnActionExecuting(_unmarkedContext);
        return _unmarkedContext.Result is null;
    }

    private static bool HasSkipAttribute(MethodInfo methodInfo)
    {
        return methodInfo.GetCustomAttributes(typeof(Ark.Tools.AspNetCore.SkipModelStateValidationFilterAttribute), true).Length > 0;
    }

    private static ActionExecutingContext CreateContext(MethodInfo methodInfo)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new Microsoft.AspNetCore.Routing.RouteData(),
            new ControllerActionDescriptor
            {
                ControllerTypeInfo = typeof(TestController).GetTypeInfo(),
                MethodInfo = methodInfo,
            });

        return new ActionExecutingContext(
            actionContext,
            Array.Empty<IFilterMetadata>(),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            null!);
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
