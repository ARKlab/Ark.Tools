using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Configuration
{
	public class SchoolModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var schools = builder.EntitySet<School>("Schools").EntityType;

			schools.Filter();

			schools.Expand();

			//schools
			//	.HasMany(x => x.Cities)
			//	.IsExpandable()
			//	;

			
		}
	}
}
