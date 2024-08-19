using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Configuration
{
	public class BookModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var books = builder.EntitySet<Book>("Books").EntityType;

			books.Filter();

			books.Expand();

			books
				.HasOptional(x => x.Press)
					.IsExpandable()
				;

			books
				.HasOptional(x => x.Audit)
					.IsExpandable()
				;

			books
				.HasOptional(x => x.Bibliografy)
					.IsExpandable()
				;
		}
	}
}
