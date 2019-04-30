using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using ODataSample.Models;

namespace ODataSample.Configuration
{
	public class AuditModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			var audit = builder.EntitySet<Audit>("Audits").EntityType;

			audit.HasKey(p => p.Id);

			audit.Filter();
			audit.Expand();
		}
	}
}
