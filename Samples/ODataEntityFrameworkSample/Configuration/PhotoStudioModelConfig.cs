using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Configuration
{
	public class PhotoStudioModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var photoStudios = builder.EntitySet<PhotoStudio>("PhotoStudios").EntityType;

			photoStudios.Filter();

			photoStudios.Expand();

			photoStudios
				.HasOptional(x => x.Audit)
					.IsExpandable()
				;
		}
	}
}
