using Asp.Versioning.OData;
using Asp.Versioning;
using Microsoft.OData.ModelBuilder;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Configuration
{
    public class PersonModelConfiguration : IModelConfiguration
    {
        public void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
        {
            if(apiVersion < ApiVersions.V2)
            {
                var person = builder.EntitySet<Person.V1>("People").EntityType;
                person.HasKey(p => p.Id);
                person.Select().OrderBy("firstName", "lastName").Filter();
            } else {
                var person = builder.EntitySet<Person.V2>("People").EntityType;
                person.HasKey(p => p.Id);
                person.Select().OrderBy("firstName", "lastName").Filter();
            }
        }
    }
}
