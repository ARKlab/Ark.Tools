using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Configuration
{
	public class OperationModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var op = builder.EntitySet<BaseOperation>("Operations").EntityType;

			op.Filter();
			op.Expand();

		//	op.CollectionProperty(x => x.Operations);
		}
	}
}
