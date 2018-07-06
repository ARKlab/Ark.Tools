using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ark.AspNetCore.Swashbuckle
{
    public static partial class Ex
    {
        public static void IncludeXmlCommentsForAssembly<T>(this SwaggerGenOptions o)
        {
            var baseDirectory = PlatformServices.Default.Application.ApplicationBasePath;
            var commentsFileName = typeof(T).Assembly.GetName().Name + ".xml";
            var path = Path.Combine(baseDirectory, commentsFileName);
            if (File.Exists(path))
                o.IncludeXmlComments(path);
        }

        public static void AddPolymorphismSupport<TBase>(this SwaggerGenOptions o, string discriminatorName = null, HashSet<Type> derivedTypes = null)
        {
            if (derivedTypes == null || derivedTypes.Count == 0)
                derivedTypes = _init<TBase>();

            o.DocumentFilter<PolymorphismDocumentFilter<TBase>>(discriminatorName);
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
    }
}
