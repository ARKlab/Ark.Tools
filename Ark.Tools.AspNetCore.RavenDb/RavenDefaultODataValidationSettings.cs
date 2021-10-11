using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;

namespace Ark.Tools.AspNetCore.RavenDb
{
	public class RavenDefaultODataValidationSettings : ODataValidationSettings
	{
		public RavenDefaultODataValidationSettings()
		{
			AllowedQueryOptions = AllowedQueryOptions.Top 
								| AllowedQueryOptions.Skip 
								| AllowedQueryOptions.Filter 
								| AllowedQueryOptions.OrderBy
								;

			AllowedFunctions = AllowedFunctions.Any
							 | AllowedFunctions.EndsWith
							 & ~(AllowedFunctions.Contains)
							 ;

			AllowedLogicalOperators = AllowedLogicalOperators.All;

			MaxAnyAllExpressionDepth = 5;

			MaxTop = 1000;
		}
	}
}
