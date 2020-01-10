// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
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

		//TODO MJ
        public static void AddPolymorphismSupport<TBase>(this SwaggerGenOptions o, string discriminatorName = "discriminator", HashSet<Type> derivedTypes = null)
        {
            if (derivedTypes == null || derivedTypes.Count == 0)
                derivedTypes = _init<TBase>();

            o.DocumentFilter<PolymorphismDocumentFilter<TBase>>(discriminatorName, derivedTypes);
            o.SchemaFilter<PolymorphismSchemaFilter<TBase>>(derivedTypes);
        }

        private static HashSet<Type> _init<TBase>()
        {
            var abstractType = typeof(TBase);
            var dTypes = abstractType.Assembly
                                     .GetTypes()
                                     .Where(x => abstractType != x && abstractType.IsAssignableFrom(x));

            var result = new HashSet<Type>();

            foreach (var item in dTypes)
                result.Add(item);

            return result;
        }

        public static IServiceCollection ArkConfigureSwagger(this IServiceCollection services, Action<SwaggerOptions> setup)
        {
            return services.Configure(setup);
        }

        public static IServiceCollection ArkConfigureSwaggerUI(this IServiceCollection services, Action<SwaggerUIOptions> setup)
        {
            return services.Configure(setup);
        }

        [Obsolete("Use UseSwagger() without a setup action", true)]
        public static IApplicationBuilder ArkUseSwagger(this IApplicationBuilder app, Action<SwaggerOptions> setup = null)
        {
            throw new NotImplementedException();
        }

        [Obsolete("Use UseSwaggerUI() without a setup action", true)]
        public static IApplicationBuilder ArkUseSwaggerUI(this IApplicationBuilder app, Action<SwaggerUIOptions> setup = null)
        {
            throw new NotImplementedException();
        }
    }
}
