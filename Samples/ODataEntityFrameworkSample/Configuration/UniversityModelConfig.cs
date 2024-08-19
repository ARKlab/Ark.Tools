using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Configuration
{
	public class UniversityModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var universities = builder.EntitySet<University>("Universities").EntityType;

			universities.Filter();

			universities.Expand();

			//schools
			//	.HasMany(x => x.Cities)
			//	.IsExpandable()
			//	;

			
		}
	}
}
