using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class PolymorphismSchemaFilter<T> : ISchemaFilter
    {
        private readonly HashSet<Type> _derivedTypes;

        public PolymorphismSchemaFilter(HashSet<Type> derivedTypes)
        {
            _derivedTypes = derivedTypes;
        }
        
        public void Apply(Schema model, SchemaFilterContext context)
        {
            if (!_derivedTypes.Contains(context.SystemType)) return;
            
            var parentSchemaRef = context.SchemaRegistry.GetOrRegister(typeof(T));
            var parentSchema = context.SchemaRegistry.Definitions[parentSchemaRef.Ref.Split('/').Last()];

            foreach (var p in parentSchema.Properties.Keys)
                model.Properties.Remove(p);

            model.AllOf = new List<Schema> { parentSchemaRef };            
        }
    }

    public class PolymorphismDocumentFilter<T> : IDocumentFilter
    {
        private readonly string _discriminatorName;

        public PolymorphismDocumentFilter(string fieldDiscriminatorName)
        {
            _discriminatorName = fieldDiscriminatorName ?? "discriminator";
        }

        public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context)
        {
            var parentSchema = context.SchemaRegistry.GetOrRegister(typeof(T));
            if (parentSchema.Ref != null)
                parentSchema = context.SchemaRegistry.Definitions[parentSchema.Ref.Split('/').Last()];

            //set up a discriminator property (it must be required)
            parentSchema.Discriminator = _discriminatorName;
            parentSchema.Required = parentSchema.Required ?? new List<string>();
            if (!parentSchema.Required.Contains(_discriminatorName))
                parentSchema.Required.Add(_discriminatorName);

            if (!parentSchema.Properties.ContainsKey(_discriminatorName))
                parentSchema.Properties.Add(_discriminatorName, new Schema { Type = "string" });

            //register all subclasses
            var derivedTypes = typeof(T).Assembly
                                           .GetTypes()
                                           .Where(x => typeof(T) != x && typeof(T).IsAssignableFrom(x));

            foreach (var item in derivedTypes)
                context.SchemaRegistry.GetOrRegister(item);
        }
    }    
}
