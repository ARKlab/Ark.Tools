using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Configuration
{
	public class CountryModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var countries = builder.EntitySet<Country>("Countries").EntityType;

			countries.Filter();

			countries.Expand();

			countries
				.HasMany(x => x.Cities)
				.IsExpandable()
				;

			
		}
	}
}
