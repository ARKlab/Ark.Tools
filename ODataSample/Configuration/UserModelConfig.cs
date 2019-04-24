using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Configuration
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
