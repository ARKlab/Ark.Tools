using Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;

namespace ODataSample.Configuration
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
		}
	}
}
