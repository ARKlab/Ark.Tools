using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;

namespace ODataSample.Configuration
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
