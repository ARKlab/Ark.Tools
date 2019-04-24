using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using RavenDbSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample.Configurations
{
	public class BaseOperationModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var op = builder.EntitySet<BaseOperation>("BaseOperations").EntityType;

			op.Select();

			op.Filter();

			op.ContainsRequired(x => x.B);

			op.ContainsMany(x => x.Operations)
				.AutomaticallyExpand(true)
				.Contained()
				.Filter()
				;
			//https://stackoverflow.com/questions/29165465/difference-between-pagesize-and-maxtop
			op.Page(1000, 100);
			op.OrderBy();
		}
	}
}
