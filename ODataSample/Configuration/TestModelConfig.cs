using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Configuration
{
	public class TestModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var test = builder.EntitySet<Test>("Tests").EntityType;

			test.Filter();
			test.OrderBy();
			test.Page();
		}
	}
}
