// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;

using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

using System.IO;
using System.Reflection;

namespace Ark.Tools.AspNetCore.Swashbuckle;

public static partial class Ex
{
    public static void IncludeXmlCommentsForAssembly<T>(this SwaggerGenOptions o) => o.IncludeXmlCommentsForAssembly(typeof(T).Assembly);

    public static void IncludeXmlCommentsForAssembly(this SwaggerGenOptions o, Assembly assembly)
    {
        var baseDirectory = System.AppContext.BaseDirectory;
        var commentsFileName = assembly.GetName().Name + ".xml";
        var path = Path.Combine(baseDirectory, commentsFileName);
        if (File.Exists(path))
            o.IncludeXmlComments(path);
    }

    public static IServiceCollection ArkConfigureSwagger(this IServiceCollection services, Action<SwaggerOptions> setup)
    {
        return services.Configure(setup);
    }

    public static IServiceCollection ArkConfigureSwaggerUI(this IServiceCollection services, Action<SwaggerUIOptions> setup)
    {
        return services.Configure(setup);
    }
}