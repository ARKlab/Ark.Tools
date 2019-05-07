using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using RavenDbSample.Models;

namespace RavenDbSample.Configurations
{
	public class UserModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var users = builder.EntitySet<User>("Users").EntityType;

			users.Filter();
			users.Expand();

			users.CollectionProperty(x => x.Roles);
		}
	}
}
