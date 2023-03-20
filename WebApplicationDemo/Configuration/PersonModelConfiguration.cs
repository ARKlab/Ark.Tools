using Asp.Versioning.OData;
using Asp.Versioning;
using Microsoft.OData.ModelBuilder;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Configuration
{
    public class PersonModelConfiguration : IModelConfiguration
    {
        /// <inheritdoc />
        public void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
        {
            var person = builder.EntitySet<Person>("People").EntityType;
            //var address = builder.EntityType<Address>().HasKey( a => a.Id );

            person.HasKey(p => p.Id);
            person.Select().OrderBy("firstName", "lastName");
        }
    }
}
